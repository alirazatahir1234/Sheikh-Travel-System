using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetTripAnalyticsQuery(int? VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<TripAnalyticsBundleDto>>;

public class GetTripAnalyticsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetTripAnalyticsQuery, ApiResponse<TripAnalyticsBundleDto>>
{
    public Task<ApiResponse<TripAnalyticsBundleDto>> Handle(GetTripAnalyticsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetTripAnalyticsAsync(request, cancellationToken);
}

public record GetTripReplayQuery(
    int? VehicleId,
    DateTime? FromDate,
    DateTime? ToDate,
    int? RouteMaxPoints = null,
    int? PlaybackMaxPoints = null,
    bool IncludeRaw = false)
    : IRequest<ApiResponse<TripReplayBundleDto>>;

public class GetTripReplayQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetTripReplayQuery, ApiResponse<TripReplayBundleDto>>
{
    public Task<ApiResponse<TripReplayBundleDto>> Handle(GetTripReplayQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetTripReplayAsync(request, cancellationToken);
}

public record GetTripDetailQuery(string TripKey) : IRequest<ApiResponse<TripDetailBundleDto>>;

public class GetTripDetailQueryHandler(IMediator mediator)
    : IRequestHandler<GetTripDetailQuery, ApiResponse<TripDetailBundleDto>>
{
    public async Task<ApiResponse<TripDetailBundleDto>> Handle(GetTripDetailQuery request, CancellationToken cancellationToken)
    {
        if (!TripKeyHelper.TryParse(request.TripKey, out var vehicleId, out var startTimeUtc))
        {
            return ApiResponse<TripDetailBundleDto>.FailResponse("Invalid trip key.");
        }

        var fromDate = startTimeUtc.AddHours(-12);
        var toDate = startTimeUtc.AddDays(2);
        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(vehicleId, fromDate, toDate, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
        {
            return ApiResponse<TripDetailBundleDto>.FailResponse(tripsResponse.Message ?? "Failed to load trip.");
        }

        var trip = tripsResponse.Data.Items.FirstOrDefault(t =>
            string.Equals(t.TripKey, request.TripKey, StringComparison.Ordinal)
            || Math.Abs((t.StartTime.ToUniversalTime() - startTimeUtc).TotalSeconds) < 2);

        if (trip is null)
        {
            return ApiResponse<TripDetailBundleDto>.FailResponse("Trip not found.");
        }

        var replayResponse = await mediator.Send(
            new GetTripReplayQuery(vehicleId, trip.StartTime, trip.EndTime),
            cancellationToken);

        if (!replayResponse.Success || replayResponse.Data is null)
        {
            return ApiResponse<TripDetailBundleDto>.FailResponse(replayResponse.Message ?? "Failed to load trip replay.");
        }

        var replay = replayResponse.Data;
        var detail = new TripDetailBundleDto(
            TraccarTripMapper.Enrich(trip),
            replay.Summary,
            replay.Stops,
            replay.Events,
            replay.Route,
            replay.Playback);

        return ApiResponse<TripDetailBundleDto>.SuccessResponse(detail);
    }
}

public record GetTripContextQuery(int VehicleId) : IRequest<ApiResponse<TripDeviceContextDto>>;

public class GetTripContextQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetTripContextQuery, ApiResponse<TripDeviceContextDto>>
{
    public Task<ApiResponse<TripDeviceContextDto>> Handle(GetTripContextQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetTripContextAsync(request, cancellationToken);
}

public record GetFleetTripSummaryQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<TripAnalyticsSummaryDto>>;

/// <summary>
/// Fleet-wide KPI cards for the Trips ledger. Distance/duration/speed/fuel prefer live Traccar
/// per-device summaries (BuildSummary's hasSummary branch) when the tenant's fleet is Traccar-linked
/// and reachable — Traccar is the source of truth for GPS telemetry, not a re-derivation. No
/// artificial fan-out cap: every Traccar-linked device in the trip set is queried concurrently via
/// Task.WhenAll (Traccar has no fleet-wide summary endpoint, only per-device). This is O(devices)
/// concurrent HTTP calls per load — acceptable at today's fleet sizes; revisit with
/// batching/caching if a tenant's fleet grows large enough to make this slow. Vehicles with no
/// TraccarDeviceId (never Traccar-linked) or when Traccar is unreachable/disabled fall back to the
/// local trip list, which is always fleet-complete regardless of Traccar status. Trip *count* stays
/// local (GpsTrips) — it was never subject to the fan-out concern, since local trip detection is
/// already fleet-complete with no capped dependency.
/// </summary>
public class GetFleetTripSummaryQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetFleetTripSummaryQuery, ApiResponse<TripAnalyticsSummaryDto>>
{
    public Task<ApiResponse<TripAnalyticsSummaryDto>> Handle(GetFleetTripSummaryQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetFleetTripSummaryAsync(request, cancellationToken);
}

public record GetGpsFleetStatusQuery : IRequest<ApiResponse<GpsFleetStatusDto>>;

public class GetGpsFleetStatusQueryHandler(
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<GetGpsFleetStatusQuery, ApiResponse<GpsFleetStatusDto>>
{
    /// <summary>~10 km/h in knots (Traccar position speed unit).</summary>
    private const double MovingSpeedKnots = 5.4;

    public async Task<ApiResponse<GpsFleetStatusDto>> Handle(GetGpsFleetStatusQuery request, CancellationToken cancellationToken)
    {
        var opts = traccarOptions.Value;
        if (!opts.IsConfigured || !opts.Enabled)
        {
            return ApiResponse<GpsFleetStatusDto>.SuccessResponse(
                new GpsFleetStatusDto(0, 0, 0, 0, 0, 0, 0, null, null));
        }

        var devicesTask = traccarClient.GetDevicesAsync(cancellationToken);
        var positionsTask = traccarClient.GetLivePositionsAsync(cancellationToken);
        await Task.WhenAll(devicesTask, positionsTask);

        var devices = (await devicesTask).Where(d => !d.Disabled).ToList();
        var positions = (await positionsTask).ToDictionary(p => p.DeviceId);

        var online = 0;
        var offline = 0;
        var moving = 0;
        var idle = 0;
        var parked = 0;
        var neverSeen = 0;
        double speedSum = 0;
        var speedCount = 0;

        foreach (var device in devices)
        {
            if (!string.Equals(device.Status, "online", StringComparison.OrdinalIgnoreCase))
            {
                if (device.LastUpdate is null) neverSeen++;
                else offline++;
                continue;
            }

            online++;
            if (!positions.TryGetValue(device.Id, out var pos))
            {
                offline++;
                online--;
                continue;
            }

            var speedKnots = pos.Speed;
            speedSum += speedKnots * 1.852;
            speedCount++;

            // Ignition OFF → Parked (ignore GPS drift). Else speed >= threshold → Moving.
            if (pos.Attributes?.Ignition == false)
                parked++;
            else if (speedKnots > MovingSpeedKnots)
                moving++;
            else
                idle++;
        }

        var todayStart = DateTime.UtcNow.Date;
        double? todayDistanceKm = null;
        if (devices.Count > 0)
        {
            var summaryTasks = devices
                .Take(20)
                .Select(d => traccarClient.GetSummaryAsync(d.Id, todayStart, DateTime.UtcNow, cancellationToken));
            var summaries = await Task.WhenAll(summaryTasks);
            var totalMeters = summaries.SelectMany(s => s).Sum(s => s.Distance);
            if (totalMeters > 0) todayDistanceKm = Math.Round(totalMeters / 1000.0, 1);
        }

        return ApiResponse<GpsFleetStatusDto>.SuccessResponse(new GpsFleetStatusDto(
            devices.Count,
            online,
            offline,
            moving,
            idle,
            parked,
            neverSeen,
            speedCount > 0 ? Math.Round(speedSum / speedCount, 1) : null,
            todayDistanceKm));
    }
}

/// <summary>Powers the Live Map screen's KPI strip — see GpsFleetStatusLocalDto for why this is a
/// separate, local-data-sourced sibling of GetGpsFleetStatusQuery, not a replacement for it.</summary>
public record GetGpsFleetStatusLocalQuery : IRequest<ApiResponse<GpsFleetStatusLocalDto>>;

public class GetGpsFleetStatusLocalQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsFleetStatusLocalQuery, ApiResponse<GpsFleetStatusLocalDto>>
{
    public Task<ApiResponse<GpsFleetStatusLocalDto>> Handle(GetGpsFleetStatusLocalQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsFleetStatusLocalAsync(request, cancellationToken);
}

public record GetGpsOperatorDashboardQuery : IRequest<ApiResponse<GpsOperatorDashboardDto>>;

public class GetGpsOperatorDashboardQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsOperatorDashboardQuery, ApiResponse<GpsOperatorDashboardDto>>
{
    public Task<ApiResponse<GpsOperatorDashboardDto>> Handle(GetGpsOperatorDashboardQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsOperatorDashboardAsync(request, cancellationToken);
}

public record PostGpsOperatorInsightsCommand(string QueryKey)
    : IRequest<ApiResponse<GpsOperatorInsightDto>>;

public class PostGpsOperatorInsightsHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<PostGpsOperatorInsightsCommand, ApiResponse<GpsOperatorInsightDto>>
{
    public Task<ApiResponse<GpsOperatorInsightDto>> Handle(PostGpsOperatorInsightsCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.PostGpsOperatorInsightsAsync(request, cancellationToken);
}

public record GetGpsFleetStatusHistoryQuery(DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<GpsFleetStatusSnapshotDto>>>;

public class GetGpsFleetStatusHistoryQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsFleetStatusHistoryQuery, ApiResponse<List<GpsFleetStatusSnapshotDto>>>
{
    public Task<ApiResponse<List<GpsFleetStatusSnapshotDto>>> Handle(GetGpsFleetStatusHistoryQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsFleetStatusHistoryAsync(request, cancellationToken);
}
