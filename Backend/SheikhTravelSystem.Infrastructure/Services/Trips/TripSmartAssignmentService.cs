using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Trips;
using SheikhTravelSystem.Application.Features.Trips.Commands;
using SheikhTravelSystem.Application.Features.Trips.SmartAssignment;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Trips;

public sealed class TripSmartAssignmentService(
    ITripSmartAssignmentRepository repository,
    ITripRepository tripRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IGoogleRoutesService routesService,
    INotificationDecisionEngine decisionEngine,
    IMediator mediator,
    ILogger<TripSmartAssignmentService> logger) : ITripSmartAssignmentService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ApiResponse<TripSmartAssignmentSuggestionsDto>> GetSuggestionsAsync(
        int tripId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var ctx = await repository.GetTripContextAsync(tenantId, tripId, cancellationToken);
        if (ctx is null)
            return ApiResponse<TripSmartAssignmentSuggestionsDto>.FailResponse("Trip not found.");

        var excludedDrivers = await repository.GetRejectedDriverIdsAsync(tenantId, tripId, cancellationToken);
        var (ranked, excluded) = await RankCandidatesAsync(ctx, excludedDrivers, cancellationToken);
        var routingNote = ranked.Count > 0 && ranked[0].TrafficSource == TripAssignmentScorer.TrafficHaversineEstimate
            ? "Google Routes unavailable — distance and ETA use straight-line estimates."
            : null;

        var exclusionCounts = excluded
            .GroupBy(e => e.Reason)
            .ToDictionary(g => g.Key, g => g.Count());

        if (SmartAssignmentPolicy.ShadowLoggingEnabled && ranked.Count > 0)
            await TryLogShadowAsync(ctx, ranked[0], cancellationToken);

        return ApiResponse<TripSmartAssignmentSuggestionsDto>.SuccessResponse(
            new TripSmartAssignmentSuggestionsDto(
                ctx.TripId,
                ctx.TripNumber,
                ranked,
                routingNote,
                SmartAssignmentPolicy.ActiveMode,
                excluded,
                exclusionCounts));
    }

    public async Task<ApiResponse<TripSmartAssignmentApplyResultDto>> AutoAssignAsync(
        int tripId,
        bool fromTripCreation = false,
        bool allowWhenSuggestOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (fromTripCreation && !SmartAssignmentPolicy.CreateTimeAutoAssignEnabled)
        {
            logger.LogInformation(
                "Create-time smart assign skipped for trip {TripId} (policy: staff confirm required)", tripId);
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse(
                "Create-time auto-assign is disabled. Confirm a recommendation on Smart Assignment.");
        }

        if (!SmartAssignmentPolicy.AutoApplyEnabled && !allowWhenSuggestOnly)
        {
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse(
                "Smart Trip Assignment is suggest-only. Confirm a ranked driver on the board (or enable AutoApply after shadow period).");
        }

        if (string.Equals(SmartAssignmentPolicy.ActiveMode, SmartAssignmentPolicy.ModeShadow, StringComparison.OrdinalIgnoreCase)
            && !allowWhenSuggestOnly)
        {
            var suggest = await GetSuggestionsAsync(tripId, cancellationToken);
            if (!suggest.Success || suggest.Data is null || suggest.Data.Candidates.Count == 0)
                return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse(
                    suggest.Message ?? "No eligible drivers for shadow log.");

            var top = suggest.Data.Candidates[0];
            return ApiResponse<TripSmartAssignmentApplyResultDto>.SuccessResponse(
                new TripSmartAssignmentApplyResultDto(
                    tripId, top.DriverId, top.DriverName, top.VehicleId, top.VehicleName,
                    top.TotalScore, top.Factors, 0),
                "Shadow mode: would-assign logged; trip driver not changed.");
        }

        var tenantId = tenantContext.GetRequiredTenantId();
        var ctx = await repository.GetTripContextAsync(tenantId, tripId, cancellationToken);
        if (ctx is null)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Trip not found.");

        if (TripLifecycle.IsTerminal(ctx.Status) || ctx.Status is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Trip is already active or closed.");

        var rejected = await repository.GetRejectedDriverIdsAsync(tenantId, tripId, cancellationToken);
        var (ranked, _) = await RankCandidatesAsync(ctx, rejected, cancellationToken);
        if (ranked.Count == 0)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("No eligible drivers found for smart assignment.");

        var best = ranked[0];
        return await ApplyCandidateAsync(ctx, best, outcome: "Proposed", isOverride: false, fromTripCreation, cancellationToken);
    }

    public async Task<ApiResponse<TripSmartAssignmentApplyResultDto>> ConfirmAsync(
        int tripId,
        ConfirmTripSmartAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DriverId <= 0)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Driver is required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var ctx = await repository.GetTripContextAsync(tenantId, tripId, cancellationToken);
        if (ctx is null)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Trip not found.");

        if (TripLifecycle.IsTerminal(ctx.Status) || ctx.Status is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Trip is already active or closed.");

        var rejected = await repository.GetRejectedDriverIdsAsync(tenantId, tripId, cancellationToken);
        var (ranked, _) = await RankCandidatesAsync(ctx, rejected, cancellationToken);
        var match = ranked.FirstOrDefault(c => c.DriverId == request.DriverId);
        if (match is null)
        {
            // Allow confirm of a driver still present as candidate row after re-score with empty reject list for overrides of filter edge cases
            var candidates = await repository.GetCandidatesAsync(
                tenantId, rejected, ctx.TripId, ctx.PlannedStart, cancellationToken);
            var row = candidates.FirstOrDefault(c => c.DriverId == request.DriverId);
            if (row is null)
                return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse(
                    "Driver is not an eligible suggestion. Choose a ranked candidate or use override.");

            var exclusion = TripAssignmentConstraints.Evaluate(row, ctx, TripDistanceKm(ctx));
            if (exclusion is not null)
                return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse(
                    $"Cannot confirm: {exclusion.Detail}");

            match = await ScoreRowAsync(row, ctx, cancellationToken);
        }

        if (request.VehicleId is int vid and > 0 && match.VehicleId != vid)
            match = match with { VehicleId = vid };

        return await ApplyCandidateAsync(ctx, match, outcome: "Confirmed", isOverride: false, fromTripCreation: false, cancellationToken);
    }

    public async Task<ApiResponse<TripSmartAssignmentApplyResultDto>> ApplyOverrideAsync(
        int tripId, OverrideTripSmartAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DriverId <= 0)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Driver is required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var ctx = await repository.GetTripContextAsync(tenantId, tripId, cancellationToken);
        if (ctx is null)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Trip not found.");

        var candidates = await repository.GetCandidatesAsync(
            tenantId, [], ctx.TripId, ctx.PlannedStart, cancellationToken);
        var match = candidates.FirstOrDefault(c => c.DriverId == request.DriverId);
        if (match is null)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Driver is not available for assignment.");

        var vehicleId = request.VehicleId ?? match.VehicleId;
        var candidate = await ScoreRowAsync(match, ctx, cancellationToken);
        if (vehicleId != match.VehicleId)
            candidate = candidate with { VehicleId = vehicleId };

        return await ApplyCandidateAsync(ctx, candidate, outcome: "Overridden", isOverride: true, fromTripCreation: false, cancellationToken,
            overrideReason: request.Reason);
    }

    public async Task<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>> GetDecisionsAsync(
        int tripId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetDecisionsForTripAsync(tenantId, tripId, cancellationToken);
        return ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>> GetRecentDecisionsAsync(
        int limit = 50, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetRecentDecisionsAsync(tenantId, limit, cancellationToken);
        return ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>.SuccessResponse(rows);
    }

    public async Task<bool> TryReassignAfterDriverRejectAsync(
        int tripId, int driverId, string? reason, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var latest = await repository.GetLatestActiveDecisionAsync(tenantId, tripId, driverId, cancellationToken);
        if (latest is null)
            return false;

        await repository.UpdateDecisionOutcomeAsync(tenantId, latest.Id, "Rejected", reason, cancellationToken);

        var clear = await tripRepository.ClearDriverAssignmentAsync(tripId, cancellationToken);
        if (!clear.Success)
        {
            logger.LogWarning("Smart assignment reject: could not clear driver on trip {TripId}: {Error}", tripId, clear.ErrorMessage);
            return false;
        }

        var rejected = await repository.GetRejectedDriverIdsAsync(tenantId, tripId, cancellationToken);
        var ctx = await repository.GetTripContextAsync(tenantId, tripId, cancellationToken);
        if (ctx is null)
            return true;

        var (ranked, _) = await RankCandidatesAsync(ctx, rejected, cancellationToken);
        if (ranked.Count == 0)
        {
            await NotifyDispatchNoDriversAsync(ctx, cancellationToken);
            return true;
        }

        // Re-offer next ranked — operational continuity after driver reject (allowed in SuggestOnly).
        var next = ranked[0];
        var apply = await ApplyCandidateAsync(ctx, next, outcome: "Proposed", isOverride: false, fromTripCreation: false, cancellationToken);
        if (!apply.Success)
            logger.LogWarning("Smart assignment re-offer failed for trip {TripId}: {Message}", tripId, apply.Message);

        return true;
    }

    private async Task<(IReadOnlyList<TripAssignmentCandidateDto> Ranked, IReadOnlyList<TripAssignmentExclusionDto> Excluded)> RankCandidatesAsync(
        TripSmartAssignmentTripContext ctx,
        IReadOnlyCollection<int> excludedDriverIds,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetCandidatesAsync(
            tenantId, excludedDriverIds, ctx.TripId, ctx.PlannedStart, cancellationToken);
        var scored = new List<TripAssignmentCandidateDto>();
        var excluded = new List<TripAssignmentExclusionDto>();
        var tripDistanceKm = TripDistanceKm(ctx);

        foreach (var row in rows)
        {
            var exclusion = TripAssignmentConstraints.Evaluate(row, ctx, tripDistanceKm);
            if (exclusion is not null)
            {
                excluded.Add(exclusion);
                continue;
            }

            scored.Add(await ScoreRowAsync(row, ctx, cancellationToken));
        }

        var ranked = scored
            .OrderByDescending(c => c.TotalScore)
            .Select((c, i) => c with { Rank = i + 1 })
            .ToList();

        return (ranked, excluded);
    }

    private async Task<TripAssignmentCandidateDto> ScoreRowAsync(
        TripAssignmentCandidateRow row,
        TripSmartAssignmentTripContext ctx,
        CancellationToken cancellationToken)
    {
        var tripInput = ToTripInput(ctx);
        var candInput = ToCandidateInput(row);
        var route = await TryRouteAsync(candInput, tripInput, cancellationToken);
        var (total, factors) = TripAssignmentScorer.Score(candInput, tripInput, route);
        return new TripAssignmentCandidateDto(
            row.DriverId,
            row.DriverName,
            row.VehicleId,
            row.VehicleName,
            total,
            0,
            route is null ? null : (decimal)(route.DistanceMeters / 1000.0),
            route is null ? null : (int)Math.Ceiling(route.DurationSeconds / 60.0),
            route?.Source ?? TripAssignmentScorer.TrafficHaversineEstimate,
            factors);
    }

    private async Task TryLogShadowAsync(
        TripSmartAssignmentTripContext ctx,
        TripAssignmentCandidateDto top,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (await repository.HasShadowDecisionTodayAsync(tenantId, ctx.TripId, cancellationToken))
            return;

        try
        {
            await repository.InsertDecisionAsync(tenantId, new TripSmartAssignmentDecisionWrite(
                ctx.TripId,
                top.DriverId,
                top.VehicleId,
                top.Rank,
                top.TotalScore,
                JsonSerializer.Serialize(top.Factors, JsonOpts),
                ctx.RouteId,
                top.DistanceKm,
                top.EtaMinutes,
                top.TrafficSource,
                "Shadow",
                "Would-assign (shadow) — trip not modified",
                currentUser.UserId?.ToString() ?? "shadow"), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Shadow decision log skipped for trip {TripId}", ctx.TripId);
        }
    }

    private async Task<ApiResponse<TripSmartAssignmentApplyResultDto>> ApplyCandidateAsync(
        TripSmartAssignmentTripContext ctx,
        TripAssignmentCandidateDto candidate,
        string outcome,
        bool isOverride,
        bool fromTripCreation,
        CancellationToken cancellationToken,
        string? overrideReason = null)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await repository.MarkPriorProposedSupersededAsync(tenantId, ctx.TripId, cancellationToken);

        var assignDriver = await mediator.Send(
            new AssignTripDriverCommand(ctx.TripId, candidate.DriverId), cancellationToken);
        if (!assignDriver.Success)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse(assignDriver.Message);

        if (candidate.VehicleId is int vehicleId and > 0)
        {
            var assignVehicle = await mediator.Send(new AssignTripVehicleCommand(ctx.TripId, vehicleId), cancellationToken);
            if (!assignVehicle.Success)
                logger.LogWarning("Smart assignment: vehicle assign failed for trip {TripId}: {Message}", ctx.TripId, assignVehicle.Message);
        }

        var attempt = (await repository.GetDecisionsForTripAsync(tenantId, ctx.TripId, cancellationToken)).Count + 1;
        var actor = currentUser.UserId?.ToString() ?? (fromTripCreation ? "auto-create" : "system");

        await repository.InsertDecisionAsync(tenantId, new TripSmartAssignmentDecisionWrite(
            ctx.TripId,
            candidate.DriverId,
            candidate.VehicleId,
            candidate.Rank,
            candidate.TotalScore,
            JsonSerializer.Serialize(candidate.Factors, JsonOpts),
            ctx.RouteId,
            candidate.DistanceKm,
            candidate.EtaMinutes,
            candidate.TrafficSource,
            outcome,
            isOverride ? overrideReason : null,
            actor), cancellationToken);

        await NotifyDriverAsync(ctx, candidate, cancellationToken);

        if (isOverride)
            await NotifyDispatchOverrideAsync(ctx, candidate, overrideReason, cancellationToken);

        return ApiResponse<TripSmartAssignmentApplyResultDto>.SuccessResponse(
            new TripSmartAssignmentApplyResultDto(
                ctx.TripId,
                candidate.DriverId,
                candidate.DriverName,
                candidate.VehicleId,
                candidate.VehicleName,
                candidate.TotalScore,
                candidate.Factors,
                attempt),
            isOverride ? "Assignment overridden."
                : outcome == "Confirmed" ? "Recommendation confirmed."
                : "Best driver assigned.");
    }

    private async Task NotifyDriverAsync(
        TripSmartAssignmentTripContext ctx, TripAssignmentCandidateDto candidate, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetCandidatesAsync(tenantId, [], ctx.TripId, ctx.PlannedStart, cancellationToken);
        var userId = rows.FirstOrDefault(r => r.DriverId == candidate.DriverId)?.UserId;
        if (userId is not > 0)
            return;

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "trip_driver_assigned",
            $"Trip assignment: {ctx.TripNumber}",
            "You were selected for this trip. Open the driver app to Accept or Reject.",
            NotificationType.TripDriverAssigned,
            ReferenceId: ctx.TripId,
            TenantId: tenantId,
            TargetUserId: userId,
            SuggestedPriority: 2,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push
            ]), cancellationToken);
    }

    private async Task NotifyDispatchOverrideAsync(
        TripSmartAssignmentTripContext ctx,
        TripAssignmentCandidateDto candidate,
        string? reason,
        CancellationToken cancellationToken)
    {
        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "trip_driver_assigned",
            $"Manual override: {ctx.TripNumber}",
            $"Admin assigned {candidate.DriverName} (score {candidate.TotalScore:F0}). {reason}".Trim(),
            NotificationType.TripDriverAssigned,
            ReferenceId: ctx.TripId,
            SuggestedPriority: 2,
            RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Browser]), cancellationToken);
    }

    private async Task NotifyDispatchNoDriversAsync(
        TripSmartAssignmentTripContext ctx, CancellationToken cancellationToken)
    {
        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "trip_created",
            $"Re-assignment needed: {ctx.TripNumber}",
            "All suggested drivers declined. Assign manually from Smart Assignment.",
            NotificationType.TripCreated,
            ReferenceId: ctx.TripId,
            SuggestedPriority: 2,
            RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Browser]), cancellationToken);
    }

    private async Task<TripAssignmentScorer.RouteMetrics?> TryRouteAsync(
        TripAssignmentScorer.TripAssignmentCandidateInput candidate,
        TripAssignmentScorer.TripAssignmentTripInput trip,
        CancellationToken cancellationToken)
    {
        if (candidate.Latitude is null || candidate.Longitude is null
            || trip.PickupLatitude is null || trip.PickupLongitude is null)
            return null;

        try
        {
            var result = await routesService.ComputeRouteAsync(
                candidate.Latitude.Value, candidate.Longitude.Value,
                trip.PickupLatitude.Value, trip.PickupLongitude.Value,
                cancellationToken: cancellationToken);
            if (result is null)
                return null;

            return new TripAssignmentScorer.RouteMetrics(
                result.DistanceMeters,
                result.DurationSeconds,
                TripAssignmentScorer.TrafficGoogleRoutes);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Routes API skipped for smart assignment scoring");
            return null;
        }
    }

    private static TripAssignmentScorer.TripAssignmentTripInput ToTripInput(TripSmartAssignmentTripContext ctx)
        => new(ctx.PlannedStart, ctx.PickupLatitude, ctx.PickupLongitude, ctx.DestinationLatitude, ctx.DestinationLongitude);

    private static TripAssignmentScorer.TripAssignmentCandidateInput ToCandidateInput(TripAssignmentCandidateRow row)
        => new(
            row.DriverId,
            row.Rating,
            row.Latitude,
            row.Longitude,
            row.FuelLevelPercent,
            row.HoursWorkedToday,
            row.VehicleFuelType == (int)FuelType.Electric,
            row.ElectricRangeKm,
            row.PerformanceScore,
            row.SafetyScore,
            row.FuelAverageKmPerLiter,
            row.RecentKm7d);

    private static double? TripDistanceKm(TripSmartAssignmentTripContext ctx)
    {
        if (ctx.PickupLatitude is null || ctx.PickupLongitude is null
            || ctx.DestinationLatitude is null || ctx.DestinationLongitude is null)
            return null;

        return GpsGeoHelper.HaversineKm(
            ctx.PickupLatitude.Value, ctx.PickupLongitude.Value,
            ctx.DestinationLatitude.Value, ctx.DestinationLongitude.Value);
    }
}
