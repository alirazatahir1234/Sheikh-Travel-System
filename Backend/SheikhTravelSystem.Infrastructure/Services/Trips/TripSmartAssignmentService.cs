using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers;
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

        var excluded = await repository.GetRejectedDriverIdsAsync(tenantId, tripId, cancellationToken);
        var ranked = await RankCandidatesAsync(ctx, excluded, cancellationToken);
        var routingNote = ranked.Count > 0 && ranked[0].TrafficSource == TripAssignmentScorer.TrafficHaversineEstimate
            ? "Google Routes unavailable — distance and ETA use straight-line estimates."
            : null;

        return ApiResponse<TripSmartAssignmentSuggestionsDto>.SuccessResponse(
            new TripSmartAssignmentSuggestionsDto(ctx.TripId, ctx.TripNumber, ranked, routingNote));
    }

    public async Task<ApiResponse<TripSmartAssignmentApplyResultDto>> AutoAssignAsync(
        int tripId, bool fromTripCreation = false, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var ctx = await repository.GetTripContextAsync(tenantId, tripId, cancellationToken);
        if (ctx is null)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Trip not found.");

        if (TripLifecycle.IsTerminal(ctx.Status) || ctx.Status is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Trip is already active or closed.");

        var excluded = await repository.GetRejectedDriverIdsAsync(tenantId, tripId, cancellationToken);
        var ranked = await RankCandidatesAsync(ctx, excluded, cancellationToken);
        if (ranked.Count == 0)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("No eligible drivers found for smart assignment.");

        var best = ranked[0];
        return await ApplyCandidateAsync(ctx, best, outcome: "Proposed", isOverride: false, fromTripCreation, cancellationToken);
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

        var candidates = await repository.GetCandidatesAsync(tenantId, [], cancellationToken);
        var match = candidates.FirstOrDefault(c => c.DriverId == request.DriverId);
        if (match is null)
            return ApiResponse<TripSmartAssignmentApplyResultDto>.FailResponse("Driver is not available for assignment.");

        var vehicleId = request.VehicleId ?? match.VehicleId;
        var tripInput = ToTripInput(ctx);
        var candInput = ToCandidateInput(match);
        var route = await TryRouteAsync(candInput, tripInput, cancellationToken);
        var (total, factors) = TripAssignmentScorer.Score(candInput, tripInput, route);
        var candidate = new TripAssignmentCandidateDto(
            match.DriverId,
            match.DriverName,
            vehicleId,
            match.VehicleName,
            total,
            1,
            route is null ? null : route.DistanceMeters / 1000m,
            route is null ? null : (int)Math.Ceiling(route.DurationSeconds / 60.0),
            route?.Source ?? TripAssignmentScorer.TrafficHaversineEstimate,
            factors);

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

        var excluded = await repository.GetRejectedDriverIdsAsync(tenantId, tripId, cancellationToken);
        var ctx = await repository.GetTripContextAsync(tenantId, tripId, cancellationToken);
        if (ctx is null)
            return true;

        var ranked = await RankCandidatesAsync(ctx, excluded, cancellationToken);
        if (ranked.Count == 0)
        {
            await NotifyDispatchNoDriversAsync(ctx, cancellationToken);
            return true;
        }

        var next = ranked[0];
        var apply = await ApplyCandidateAsync(ctx, next, outcome: "Proposed", isOverride: false, fromTripCreation: false, cancellationToken);
        if (!apply.Success)
            logger.LogWarning("Smart assignment re-offer failed for trip {TripId}: {Message}", tripId, apply.Message);

        return true;
    }

    private async Task<IReadOnlyList<TripAssignmentCandidateDto>> RankCandidatesAsync(
        TripSmartAssignmentTripContext ctx,
        IReadOnlyCollection<int> excludedDriverIds,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetCandidatesAsync(tenantId, excludedDriverIds, cancellationToken);
        var tripInput = ToTripInput(ctx);
        var scored = new List<TripAssignmentCandidateDto>();

        var tripDistanceKm = TripDistanceKm(ctx);

        foreach (var row in rows)
        {
            if (!IsEligible(row))
                continue;

            if (!HasSufficientEvRange(row, tripDistanceKm))
                continue;

            var candInput = ToCandidateInput(row);
            var route = await TryRouteAsync(candInput, tripInput, cancellationToken);
            var (total, factors) = TripAssignmentScorer.Score(candInput, tripInput, route);
            scored.Add(new TripAssignmentCandidateDto(
                row.DriverId,
                row.DriverName,
                row.VehicleId,
                row.VehicleName,
                total,
                0,
                route is null ? null : (decimal)(route.DistanceMeters / 1000.0),
                route is null ? null : (int)Math.Ceiling(route.DurationSeconds / 60.0),
                route?.Source ?? TripAssignmentScorer.TrafficHaversineEstimate,
                factors));
        }

        return scored
            .OrderByDescending(c => c.TotalScore)
            .Select((c, i) => c with { Rank = i + 1 })
            .ToList();
    }

    private static bool IsEligible(TripAssignmentCandidateRow row)
    {
        try
        {
            DriverAssignmentGuard.EnsureAssignableForVehicleTrip(
                row.IsActive,
                (DriverStatus)row.Status,
                row.VerificationStatus,
                row.LicenseExpiryDate);
            return true;
        }
        catch
        {
            return false;
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
            isOverride ? "Assignment overridden." : "Best driver assigned.");
    }

    private async Task NotifyDriverAsync(
        TripSmartAssignmentTripContext ctx, TripAssignmentCandidateDto candidate, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await repository.GetCandidatesAsync(tenantId, [], cancellationToken);
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
            row.ElectricRangeKm);

    private static double? TripDistanceKm(TripSmartAssignmentTripContext ctx)
    {
        if (ctx.PickupLatitude is null || ctx.PickupLongitude is null
            || ctx.DestinationLatitude is null || ctx.DestinationLongitude is null)
            return null;

        return GpsGeoHelper.HaversineKm(
            ctx.PickupLatitude.Value, ctx.PickupLongitude.Value,
            ctx.DestinationLatitude.Value, ctx.DestinationLongitude.Value);
    }

    private static bool HasSufficientEvRange(TripAssignmentCandidateRow row, double? tripDistanceKm)
    {
        if (row.VehicleFuelType != (int)FuelType.Electric || !tripDistanceKm.HasValue)
            return true;

        if (!row.ElectricRangeKm.HasValue || row.ElectricRangeKm <= 0)
            return true;

        var batteryFactor = row.FuelLevelPercent.HasValue
            ? row.FuelLevelPercent.Value / 100m
            : 0.6m;
        var estimatedKm = (double)(row.ElectricRangeKm.Value * batteryFactor);
        return estimatedKm >= tripDistanceKm.Value * 1.15;
    }
}
