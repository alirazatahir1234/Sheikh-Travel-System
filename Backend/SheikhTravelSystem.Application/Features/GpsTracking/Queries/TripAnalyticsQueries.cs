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

        var tripsTask = mediator.Send(new GetGpsTripsQuery(vehicleId, fromDate, toDate), cancellationToken);
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

        var trips = tripsResponse.Data;
        var summary = TripAnalyticsMapper.BuildSummary(trips, summaries, stops, events);
        var eventDtos = events.Select(TripAnalyticsMapper.ToEventDto).OrderByDescending(e => e.Time).ToList();
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
            var deviceId = source.TraccarDeviceId.Value;
            var routeTask = traccarClient.GetRouteAsync(deviceId, fromDate, toDate, cancellationToken);
            var stopsTask = traccarClient.GetStopsAsync(deviceId, fromDate, toDate, cancellationToken);
            var eventsTask = traccarClient.GetEventsAsync(deviceId, fromDate, toDate, ct: cancellationToken);
            var summaryTask = traccarClient.GetSummaryAsync(deviceId, fromDate, toDate, cancellationToken);
            await Task.WhenAll(routeTask, stopsTask, eventsTask, summaryTask);

            var routeFull = (await routeTask)
                .Select(TripAnalyticsMapper.ToReplayPosition)
                .OrderBy(p => p.Timestamp)
                .ToList();
            var route = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: 2000);
            var playback = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: 800);

            var stops = (await stopsTask)
                .Where(s => TripAnalyticsMapper.OverlapsWindow(s.StartTime, s.EndTime, fromDate, toDate))
                .Select(TripAnalyticsMapper.ToStopDto)
                .OrderBy(s => s.StartTime)
                .ToList();

            var events = (await eventsTask)
                .Where(e => e.EventTime >= fromDate && e.EventTime <= toDate)
                .Select(TripAnalyticsMapper.ToEventDto)
                .OrderBy(e => e.Time)
                .ToList();

            var summaries = (await summaryTask).ToArray();
            var replaySummary = TripAnalyticsMapper.BuildReplaySummary(summaries, route);

            return ApiResponse<TripReplayBundleDto>.SuccessResponse(
                new TripReplayBundleDto(route, playback, stops, events, replaySummary));
        }

        var history = await mediator.Send(new GetPositionHistoryQuery(vehicleId, fromDate, toDate), cancellationToken);
        if (!history.Success || history.Data is null)
        {
            return ApiResponse<TripReplayBundleDto>.FailResponse(history.Message ?? "Failed to load replay positions.");
        }

        var fallbackRoute = TripAnalyticsMapper.DownsampleReplay(
            history.Data.Select(TripAnalyticsMapper.ToReplayPosition).OrderBy(p => p.Timestamp).ToList());
        var fallbackPlayback = TripAnalyticsMapper.DownsampleReplay(fallbackRoute, maxPoints: 800);

        return ApiResponse<TripReplayBundleDto>.SuccessResponse(
            new TripReplayBundleDto(
                fallbackRoute,
                fallbackPlayback,
                [],
                [],
                TripAnalyticsMapper.BuildReplaySummary([], fallbackRoute)));
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
/// Fleet-wide KPI cards for the Trips ledger. Distance/duration/speed/fuel are summed directly
/// from the (already fleet-complete, local+Traccar+detector-merged) trip list rather than reused
/// from <see cref="GetGpsFleetStatusQuery"/>'s per-vehicle Traccar summaries — those only cover a
/// capped subset of devices and would under-count a large fleet. Stop/idle/overspeed/harsh-event
/// counts still require a capped Traccar events/stops fan-out since there's no local equivalent.
/// </summary>
public class GetFleetTripSummaryQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetFleetTripSummaryQuery, ApiResponse<TripAnalyticsSummaryDto>>
{
    private const int MaxSummaryFetches = 20;

    public async Task<ApiResponse<TripAnalyticsSummaryDto>> Handle(GetFleetTripSummaryQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
        {
            return ApiResponse<TripAnalyticsSummaryDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");
        }

        var trips = tripsResponse.Data;
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
                cancellationToken: cancellationToken))).Take(MaxSummaryFetches).ToList();

            if (deviceIds.Count > 0)
            {
                var stopsTasks = deviceIds.Select(id => traccarClient.GetStopsAsync(id, fromDate, toDate, cancellationToken));
                var eventsTasks = deviceIds.Select(id => traccarClient.GetEventsAsync(id, fromDate, toDate, ct: cancellationToken));
                var allStops = await Task.WhenAll(stopsTasks);
                var allEvents = await Task.WhenAll(eventsTasks);
                stops = allStops.SelectMany(s => s).ToArray();
                events = allEvents.SelectMany(e => e).ToArray();
            }
        }

        // Empty summaries forces BuildSummary's trips-derived branch for distance/duration/speed,
        // which is fleet-complete — the capped stops/events fetch above only feeds the
        // stop/idle/overspeed/harsh-event counts, which have no local-data equivalent anyway.
        var summary = TripAnalyticsMapper.BuildSummary(trips, [], stops, events);

        return ApiResponse<TripAnalyticsSummaryDto>.SuccessResponse(summary);
    }
}

public record GetFleetStopsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null,
    int Page = 1,
    int PageSize = 25)
    : IRequest<ApiResponse<FleetReportPageDto<FleetTripStopDto>>>;

/// <summary>
/// Fleet-wide Stops report. No local persistence tier exists for stops — resolves the
/// filtered/scoped vehicle set (shared with GetGpsTripsQuery's fleet-wide path), then a capped
/// parallel Traccar fan-out, with GpsStopDetector as a last-resort fallback for vehicles neither
/// Traccar-linked nor covered by the cap. Pagination is in-memory over this capped result, not
/// true DB pagination — see FleetReportPageDto for how the UI should surface that.
/// </summary>
public class GetFleetStopsQueryHandler(
    IDbConnectionFactory dbFactory,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetFleetStopsQuery, ApiResponse<FleetReportPageDto<FleetTripStopDto>>>
{
    private const int MaxStopsFanout = 20;

    public async Task<ApiResponse<FleetReportPageDto<FleetTripStopDto>>> Handle(GetFleetStopsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : Math.Min(request.PageSize, 500);

        int? driverId = request.DriverId;
        if (currentUser.Role == "Driver")
        {
            if (!currentUser.DriverId.HasValue)
            {
                return ApiResponse<FleetReportPageDto<FleetTripStopDto>>.SuccessResponse(
                    new FleetReportPageDto<FleetTripStopDto>([], 0, page, pageSize, 0, 0));
            }

            driverId = currentUser.DriverId.Value;
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicles = await TripVehicleQueryHelper.ResolveFleetVehiclesAsync(
            connection, tenantId, request.BranchId, request.DepartmentId, driverId, fromDate, toDate, cancellationToken);

        if (vehicles.Count == 0)
        {
            return ApiResponse<FleetReportPageDto<FleetTripStopDto>>.SuccessResponse(
                new FleetReportPageDto<FleetTripStopDto>([], 0, page, pageSize, 0, 0));
        }

        var items = new List<FleetTripStopDto>();
        var opts = traccarOptions.Value;
        var traccarCandidates = opts.IsConfigured && opts.Enabled
            ? vehicles.Where(v => v.TraccarDeviceId.HasValue).Take(MaxStopsFanout).ToList()
            : [];

        if (traccarCandidates.Count > 0)
        {
            var traccarResults = await Task.WhenAll(traccarCandidates.Select(async v =>
            {
                var stops = await traccarClient.GetStopsAsync(v.TraccarDeviceId!.Value, fromDate, toDate, cancellationToken);
                return stops.Select(TripAnalyticsMapper.ToStopDto).Select(s => new FleetTripStopDto(
                    v.VehicleId, v.VehicleName, v.PlateNumber, v.DriverName,
                    s.StartTime, s.EndTime, s.Latitude, s.Longitude, s.Address, s.DurationMinutes));
            }));

            items.AddRange(traccarResults.SelectMany(r => r));
        }

        // Last-resort local fallback: vehicles neither Traccar-linked nor covered by the fan-out cap.
        var traccarCoveredIds = traccarCandidates.Select(v => v.VehicleId).ToHashSet();
        var detectorCandidates = vehicles.Where(v => !traccarCoveredIds.Contains(v.VehicleId)).Take(MaxStopsFanout).ToList();
        if (detectorCandidates.Count > 0)
        {
            var detectorVehicleIds = detectorCandidates.Select(v => v.VehicleId).ToList();
            var positions = (await connection.QueryAsync<PositionDto>(new CommandDefinition(
                """
                SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                       Heading, Altitude, Ignition, RecordedAt AS Timestamp
                FROM GpsPositions
                WHERE VehicleId IN @VehicleIds AND RecordedAt BETWEEN @FromDate AND @ToDate
                ORDER BY VehicleId, RecordedAt ASC
                """,
                new { VehicleIds = detectorVehicleIds, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

            var positionsByVehicle = positions.GroupBy(p => p.VehicleId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var v in detectorCandidates)
            {
                if (!positionsByVehicle.TryGetValue(v.VehicleId, out var vehiclePositions))
                {
                    continue;
                }

                items.AddRange(GpsStopDetector.DetectStops(vehiclePositions).Select(s =>
                    new FleetTripStopDto(v.VehicleId, v.VehicleName, v.PlateNumber, v.DriverName,
                        s.StartTime, s.EndTime, s.Latitude, s.Longitude, s.Address, s.DurationMinutes)));
            }
        }

        // Stops have no BookingId, so only the time-bounded AssignmentHistory signal applies.
        if (driverId.HasValue)
        {
            var windows = await TripVehicleQueryHelper.GetDriverAssignmentWindowsAsync(
                connection, driverId.Value, vehicles.Select(v => v.VehicleId).ToList(), fromDate, toDate, cancellationToken);
            items = items.Where(s => TripVehicleQueryHelper.MatchesDriverWindow(windows, s.VehicleId, s.StartTime)).ToList();
        }

        var ordered = items.OrderByDescending(s => s.StartTime).ToList();
        var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var vehiclesQueried = traccarCandidates.Count + detectorCandidates.Count;

        return ApiResponse<FleetReportPageDto<FleetTripStopDto>>.SuccessResponse(
            new FleetReportPageDto<FleetTripStopDto>(paged, ordered.Count, page, pageSize, vehicles.Count, vehiclesQueried));
    }
}

public record GetFleetEventsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null,
    string? EventType = null,
    int Page = 1,
    int PageSize = 25)
    : IRequest<ApiResponse<FleetReportPageDto<FleetTripEventDto>>>;

/// <summary>
/// Fleet-wide Events report. No local equivalent exists — GpsAlertEvents only persists a handful
/// of Traccar event types (device online/offline, geofence enter/exit, alarm), not the
/// overspeed/harsh-braking/harsh-acceleration types this report needs — so this is capped Traccar
/// fan-out only, with no fallback tier at all (unlike Stops, which has GpsStopDetector).
/// </summary>
public class GetFleetEventsQueryHandler(
    IDbConnectionFactory dbFactory,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetFleetEventsQuery, ApiResponse<FleetReportPageDto<FleetTripEventDto>>>
{
    private const int MaxEventsFanout = 20;

    public async Task<ApiResponse<FleetReportPageDto<FleetTripEventDto>>> Handle(GetFleetEventsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : Math.Min(request.PageSize, 500);

        int? driverId = request.DriverId;
        if (currentUser.Role == "Driver")
        {
            if (!currentUser.DriverId.HasValue)
            {
                return ApiResponse<FleetReportPageDto<FleetTripEventDto>>.SuccessResponse(
                    new FleetReportPageDto<FleetTripEventDto>([], 0, page, pageSize, 0, 0));
            }

            driverId = currentUser.DriverId.Value;
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicles = await TripVehicleQueryHelper.ResolveFleetVehiclesAsync(
            connection, tenantId, request.BranchId, request.DepartmentId, driverId, fromDate, toDate, cancellationToken);

        if (vehicles.Count == 0)
        {
            return ApiResponse<FleetReportPageDto<FleetTripEventDto>>.SuccessResponse(
                new FleetReportPageDto<FleetTripEventDto>([], 0, page, pageSize, 0, 0));
        }

        var items = new List<FleetTripEventDto>();
        var opts = traccarOptions.Value;
        var traccarCandidates = opts.IsConfigured && opts.Enabled
            ? vehicles.Where(v => v.TraccarDeviceId.HasValue).Take(MaxEventsFanout).ToList()
            : [];

        if (traccarCandidates.Count > 0)
        {
            // type isn't passed to Traccar here — its exact event-type spelling isn't confirmed
            // against the live server, so we fetch unfiltered and match defensively in C# below
            // (same Contains(..., OrdinalIgnoreCase) idiom TripAnalyticsMapper.CountEvents uses).
            var traccarResults = await Task.WhenAll(traccarCandidates.Select(async v =>
            {
                var events = await traccarClient.GetEventsAsync(v.TraccarDeviceId!.Value, fromDate, toDate, ct: cancellationToken);
                return events.Select(TripAnalyticsMapper.ToEventDto).Select(e => new FleetTripEventDto(
                    v.VehicleId, v.VehicleName, v.PlateNumber, v.DriverName,
                    e.Time, e.Type, e.Latitude, e.Longitude, e.Address, e.SpeedKmh));
            }));

            items.AddRange(traccarResults.SelectMany(r => r));
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            items = items.Where(e => e.Type.Contains(request.EventType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Events have no BookingId, so only the time-bounded AssignmentHistory signal applies.
        if (driverId.HasValue)
        {
            var windows = await TripVehicleQueryHelper.GetDriverAssignmentWindowsAsync(
                connection, driverId.Value, vehicles.Select(v => v.VehicleId).ToList(), fromDate, toDate, cancellationToken);
            items = items.Where(e => TripVehicleQueryHelper.MatchesDriverWindow(windows, e.VehicleId, e.Time)).ToList();
        }

        var ordered = items.OrderByDescending(e => e.Time).ToList();
        var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return ApiResponse<FleetReportPageDto<FleetTripEventDto>>.SuccessResponse(
            new FleetReportPageDto<FleetTripEventDto>(paged, ordered.Count, page, pageSize, vehicles.Count, traccarCandidates.Count));
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

            var ignition = pos.Attributes?.Ignition == true;
            var speedKnots = pos.Speed;
            speedSum += speedKnots * 1.852;
            speedCount++;

            if (speedKnots > MovingSpeedKnots && ignition)
                moving++;
            else if (ignition)
                idle++;
            else
                parked++;
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
