using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetTripAnalyticsQuery(int? VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<TripAnalyticsBundleDto>>;

public class GetTripAnalyticsQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetTripAnalyticsQuery, ApiResponse<TripAnalyticsBundleDto>>
{
    public async Task<ApiResponse<TripAnalyticsBundleDto>> Handle(GetTripAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var validation = TripVehicleQueryHelper.ValidateTripRequest<TripAnalyticsBundleDto>(request.VehicleId, fromDate, toDate);
        if (validation is not null) return validation;

        var vehicleId = request.VehicleId!.Value;
        var source = await TripVehicleQueryHelper.ResolveVehicleAsync(dbFactory, tenantContext, vehicleId, cancellationToken);
        if (source is null) return ApiResponse<TripAnalyticsBundleDto>.FailResponse("Vehicle not found.");

        var tripsTask = mediator.Send(new GetGpsTripsQuery(vehicleId, fromDate, toDate, Unpaged: true), cancellationToken);
        var summaries = Array.Empty<TraccarSummary>();
        var stops = Array.Empty<TraccarStop>();
        var events = Array.Empty<TraccarEvent>();

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            var deviceId = source.TraccarDeviceId.Value;
            var summaryTask = traccarClient.GetSummaryAsync(deviceId, fromDate, toDate, cancellationToken);
            var stopsTask = traccarClient.GetStopsAsync(deviceId, fromDate, toDate, cancellationToken);
            var eventsTask = traccarClient.GetEventsAsync(deviceId, fromDate, toDate, ct: cancellationToken);
            await Task.WhenAll(tripsTask, summaryTask, stopsTask, eventsTask);
            summaries = (await summaryTask).ToArray();
            stops = (await stopsTask).ToArray();
            events = (await eventsTask).ToArray();
        }
        else
        {
            await tripsTask;
        }

        var tripsResponse = await tripsTask;
        if (!tripsResponse.Success || tripsResponse.Data is null)
        {
            return ApiResponse<TripAnalyticsBundleDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");
        }

        var trips = tripsResponse.Data.Items;
        var summary = TripAnalyticsMapper.BuildSummary(trips, summaries, stops, events);
        var eventDtos = events.Select(e => TripAnalyticsMapper.ToEventDto(e)).OrderByDescending(e => e.Time).ToList();
        var stopDtos = stops.Select(TripAnalyticsMapper.ToStopDto).OrderByDescending(s => s.EndTime).ToList();

        return ApiResponse<TripAnalyticsBundleDto>.SuccessResponse(
            new TripAnalyticsBundleDto(summary, eventDtos, stopDtos));
    }
}

public record GetTripReplayQuery(int? VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<TripReplayBundleDto>>;

public class GetTripReplayQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetTripReplayQuery, ApiResponse<TripReplayBundleDto>>
{
    public async Task<ApiResponse<TripReplayBundleDto>> Handle(GetTripReplayQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var validation = TripVehicleQueryHelper.ValidateTripRequest<TripReplayBundleDto>(request.VehicleId, fromDate, toDate);
        if (validation is not null) return validation;

        var vehicleId = request.VehicleId!.Value;
        var source = await TripVehicleQueryHelper.ResolveVehicleAsync(dbFactory, tenantContext, vehicleId, cancellationToken);
        if (source is null) return ApiResponse<TripReplayBundleDto>.FailResponse("Vehicle not found.");

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            var bundle = await TripReplayLoader.LoadFromTraccarAsync(
                traccarClient,
                source.TraccarDeviceId.Value,
                fromDate,
                toDate,
                cancellationToken);
            return ApiResponse<TripReplayBundleDto>.SuccessResponse(bundle);
        }

        var localBundle = await TripReplayLoader.LoadFromLocalHistoryAsync(
            mediator, vehicleId, fromDate, toDate, cancellationToken);
        if (localBundle.Route.Count == 0 && localBundle.Playback.Count == 0)
        {
            return ApiResponse<TripReplayBundleDto>.FailResponse("Failed to load replay positions.");
        }

        return ApiResponse<TripReplayBundleDto>.SuccessResponse(localBundle);
    }
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

public class GetTripContextQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTripContextQuery, ApiResponse<TripDeviceContextDto>>
{
    public async Task<ApiResponse<TripDeviceContextDto>> Handle(GetTripContextQuery request, CancellationToken cancellationToken)
    {
        var context = await TripVehicleQueryHelper.BuildContextAsync(
            dbFactory, tenantContext, request.VehicleId, cancellationToken);
        if (context is null)
        {
            return ApiResponse<TripDeviceContextDto>.FailResponse("Vehicle not found.");
        }

        return ApiResponse<TripDeviceContextDto>.SuccessResponse(context);
    }
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
public class GetFleetTripSummaryQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetFleetTripSummaryQuery, ApiResponse<TripAnalyticsSummaryDto>>
{
    public async Task<ApiResponse<TripAnalyticsSummaryDto>> Handle(GetFleetTripSummaryQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
        {
            return ApiResponse<TripAnalyticsSummaryDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");
        }

        var trips = tripsResponse.Data.Items;
        var summaries = Array.Empty<TraccarSummary>();
        var stops = Array.Empty<TraccarStop>();
        var events = Array.Empty<TraccarEvent>();

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && trips.Count > 0)
        {
            using var connection = dbFactory.CreateConnection();
            var tenantId = tenantContext.GetRequiredTenantId();

            var deviceIds = (await connection.QueryAsync<int>(new CommandDefinition(
                """
                SELECT DISTINCT d.TraccarDeviceId
                FROM Vehicles v
                INNER JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
                WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
                  AND v.Id IN @VehicleIds
                """,
                new { TenantId = tenantId, VehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList() },
                cancellationToken: cancellationToken))).ToList();

            if (deviceIds.Count > 0)
            {
                var summaryTasks = deviceIds.Select(id => traccarClient.GetSummaryAsync(id, fromDate, toDate, cancellationToken));
                var stopsTasks = deviceIds.Select(id => traccarClient.GetStopsAsync(id, fromDate, toDate, cancellationToken));
                var eventsTasks = deviceIds.Select(id => traccarClient.GetEventsAsync(id, fromDate, toDate, ct: cancellationToken));

                var allSummaries = await Task.WhenAll(summaryTasks);
                var allStops = await Task.WhenAll(stopsTasks);
                var allEvents = await Task.WhenAll(eventsTasks);

                summaries = allSummaries.SelectMany(s => s).ToArray();
                stops = allStops.SelectMany(s => s).ToArray();
                events = allEvents.SelectMany(e => e).ToArray();
            }
        }

        var summary = TripAnalyticsMapper.BuildSummary(trips, summaries, stops, events);

        return ApiResponse<TripAnalyticsSummaryDto>.SuccessResponse(summary);
    }
}

public record GetGpsFleetStatusQuery : IRequest<ApiResponse<GpsFleetStatusDto>>;

public class GetGpsFleetStatusQueryHandler(
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<GetGpsFleetStatusQuery, ApiResponse<GpsFleetStatusDto>>
{
    private const double MovingSpeedKnots = 2.0;

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

            // Speed wins over Ignition OFF (align with resolveFleetStatus / local calculator).
            if (speedKnots > MovingSpeedKnots)
                moving++;
            else if (pos.Attributes?.Ignition == false)
                parked++;
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

public class GetGpsFleetStatusLocalQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<GetGpsFleetStatusLocalQuery, ApiResponse<GpsFleetStatusLocalDto>>
{
    public async Task<ApiResponse<GpsFleetStatusLocalDto>> Handle(GetGpsFleetStatusLocalQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var result = await GpsFleetStatusCalculator.ComputeAsync(connection, tenantId, cancellationToken);
        return ApiResponse<GpsFleetStatusLocalDto>.SuccessResponse(result);
    }
}

public record GetGpsFleetStatusHistoryQuery(DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<GpsFleetStatusSnapshotDto>>>;

public class GetGpsFleetStatusHistoryQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<GetGpsFleetStatusHistoryQuery, ApiResponse<List<GpsFleetStatusSnapshotDto>>>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(90);

    public async Task<ApiResponse<List<GpsFleetStatusSnapshotDto>>> Handle(GetGpsFleetStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
        {
            return ApiResponse<List<GpsFleetStatusSnapshotDto>>.FailResponse("'from' must be before 'to'.");
        }

        if (toDate - fromDate > MaxRange)
        {
            return ApiResponse<List<GpsFleetStatusSnapshotDto>>.FailResponse("Date range cannot exceed 90 days.");
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = await connection.QueryAsync<GpsFleetStatusSnapshotDto>(new CommandDefinition(
            """
            SELECT SnapshotAt, TotalVehicles, Online, Offline, Moving, Idle, Parked, NeverSeen, Sos, AlertsToday
            FROM GpsFleetStatusSnapshots
            WHERE TenantId = @TenantId AND SnapshotAt BETWEEN @FromDate AND @ToDate
            ORDER BY SnapshotAt ASC
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsFleetStatusSnapshotDto>>.SuccessResponse(rows.ToList());
    }
}
