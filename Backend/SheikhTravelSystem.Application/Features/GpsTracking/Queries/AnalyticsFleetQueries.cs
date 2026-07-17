using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetDistanceAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<DistanceAnalyticsDto>>;

/// <summary>
/// Day-bucketed distance trend from local GpsTrips, not live Traccar — a per-day breakdown would
/// mean one Traccar summary call per device PER DAY in range (devices × days), unlike the overview's
/// fleet-wide totals which are one call per device for the whole range. That fan-out multiplication
/// is a real cost a trend chart shouldn't pay; GpsTrips already holds the same underlying data
/// (ingested via Traccar sync or direct push, either way) and day-grouping it locally is cheap.
/// </summary>
public class GetDistanceAnalyticsQueryHandler(IMediator mediator)
    : IRequestHandler<GetDistanceAnalyticsQuery, ApiResponse<DistanceAnalyticsDto>>
{
    public async Task<ApiResponse<DistanceAnalyticsDto>> Handle(GetDistanceAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<DistanceAnalyticsDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;

        var daily = trips
            .GroupBy(t => t.StartTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyMetricDto(
                g.Key,
                (decimal)g.Sum(t => t.DistanceKm),
                g.Count(),
                Math.Round(g.Average(t => t.AvgSpeedKmh), 1)))
            .ToList();

        var dto = new DistanceAnalyticsDto(daily, (decimal)trips.Sum(t => t.DistanceKm), trips.Count);
        return ApiResponse<DistanceAnalyticsDto>.SuccessResponse(dto);
    }
}

public record GetSpeedAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<SpeedAnalyticsDto>>;

/// <summary>Same local-GpsTrips reasoning as GetDistanceAnalyticsQuery — a per-day speed trend has the same fan-out cost problem.</summary>
public class GetSpeedAnalyticsQueryHandler(IMediator mediator)
    : IRequestHandler<GetSpeedAnalyticsQuery, ApiResponse<SpeedAnalyticsDto>>
{
    private static readonly (decimal Min, decimal Max, string Label)[] Buckets =
    [
        (0, 20, "0-20"), (20, 40, "20-40"), (40, 60, "40-60"), (60, 80, "60-80"),
        (80, 100, "80-100"), (100, 120, "100-120"), (120, decimal.MaxValue, "120+")
    ];

    public async Task<ApiResponse<SpeedAnalyticsDto>> Handle(GetSpeedAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<SpeedAnalyticsDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;

        var histogram = Buckets
            .Select(b => new SpeedHistogramBucketDto(
                b.Label,
                trips.Count(t => t.AvgSpeedKmh >= b.Min && t.AvgSpeedKmh < b.Max)))
            .ToList();

        var dailyAvgSpeed = trips
            .GroupBy(t => t.StartTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyMetricDto(
                g.Key,
                (decimal)g.Sum(t => t.DistanceKm),
                g.Count(),
                Math.Round(g.Average(t => t.AvgSpeedKmh), 1)))
            .ToList();

        var dto = new SpeedAnalyticsDto(
            trips.Count > 0 ? Math.Round(trips.Average(t => t.AvgSpeedKmh), 1) : 0,
            trips.Count > 0 ? trips.Max(t => t.MaxSpeedKmh) : 0,
            histogram,
            dailyAvgSpeed);

        return ApiResponse<SpeedAnalyticsDto>.SuccessResponse(dto);
    }
}

public record GetIdleAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<IdleAnalyticsDto>>;

/// <summary>
/// Live Traccar stops per device (uncapped — one call per device for the whole range, same cost
/// profile as GetFleetTripSummaryQuery's stops fetch, not the day-multiplied cost the trend charts
/// above avoid). No local idle detector exists, so vehicles with no Traccar link are simply absent
/// from the numbers — IsPartial flags that rather than letting it look complete.
/// </summary>
public class GetIdleAnalyticsQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetIdleAnalyticsQuery, ApiResponse<IdleAnalyticsDto>>
{
    public async Task<ApiResponse<IdleAnalyticsDto>> Handle(GetIdleAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<IdleAnalyticsDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;
        var vehicleNames = trips.GroupBy(t => t.VehicleId).ToDictionary(g => g.Key, g => g.First().VehicleName);

        var opts = traccarOptions.Value;
        if (!opts.IsConfigured || !opts.Enabled || trips.Count == 0)
        {
            return ApiResponse<IdleAnalyticsDto>.SuccessResponse(new IdleAnalyticsDto(0, 0, [], trips.Count > 0));
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();

        var deviceMap = await GpsTraccarFleetFetcher.ResolveVehicleToDeviceMapAsync(connection, tenantId, vehicleIds, cancellationToken);
        var isPartial = await GpsTraccarFleetFetcher.HasNonTraccarVehicleAsync(connection, tenantId, vehicleIds, cancellationToken);

        if (deviceMap.Count == 0)
        {
            return ApiResponse<IdleAnalyticsDto>.SuccessResponse(new IdleAnalyticsDto(0, 0, [], true));
        }

        var deviceToVehicle = deviceMap.ToDictionary(kv => kv.Value, kv => kv.Key);
        var stopsTasks = deviceMap.Values.Select(id => traccarClient.GetStopsAsync(id, fromDate, toDate, cancellationToken));
        var allStops = (await Task.WhenAll(stopsTasks)).SelectMany(s => s).ToList();

        var perVehicleMinutes = new Dictionary<int, int>();
        var longest = 0;
        foreach (var stop in allStops)
        {
            var minutes = stop.Duration >= 100_000 ? stop.Duration / 60_000 : Math.Max(1, stop.Duration / 60);
            longest = Math.Max(longest, minutes);
            if (deviceToVehicle.TryGetValue(stop.DeviceId, out var vehicleId))
            {
                perVehicleMinutes[vehicleId] = perVehicleMinutes.GetValueOrDefault(vehicleId) + minutes;
            }
        }

        var topIdle = perVehicleMinutes
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new VehicleIdleDto(kv.Key, vehicleNames.GetValueOrDefault(kv.Key), kv.Value))
            .ToList();

        var dto = new IdleAnalyticsDto(perVehicleMinutes.Values.Sum(), longest, topIdle, isPartial);
        return ApiResponse<IdleAnalyticsDto>.SuccessResponse(dto);
    }
}

public record GetStopAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<StopAnalyticsDto>>;

public class GetStopAnalyticsQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetStopAnalyticsQuery, ApiResponse<StopAnalyticsDto>>
{
    public async Task<ApiResponse<StopAnalyticsDto>> Handle(GetStopAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, request.DriverId, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<StopAnalyticsDto>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;
        var opts = traccarOptions.Value;
        if (!opts.IsConfigured || !opts.Enabled || trips.Count == 0)
        {
            return ApiResponse<StopAnalyticsDto>.SuccessResponse(new StopAnalyticsDto(0, 0, 0, trips.Count > 0));
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();

        var deviceMap = await GpsTraccarFleetFetcher.ResolveVehicleToDeviceMapAsync(connection, tenantId, vehicleIds, cancellationToken);
        var isPartial = await GpsTraccarFleetFetcher.HasNonTraccarVehicleAsync(connection, tenantId, vehicleIds, cancellationToken);

        if (deviceMap.Count == 0)
        {
            return ApiResponse<StopAnalyticsDto>.SuccessResponse(new StopAnalyticsDto(0, 0, 0, true));
        }

        var stopsTasks = deviceMap.Values.Select(id => traccarClient.GetStopsAsync(id, fromDate, toDate, cancellationToken));
        var allStops = (await Task.WhenAll(stopsTasks)).SelectMany(s => s).ToList();

        if (allStops.Count == 0)
        {
            return ApiResponse<StopAnalyticsDto>.SuccessResponse(new StopAnalyticsDto(0, 0, 0, isPartial));
        }

        var durations = allStops
            .Select(s => s.Duration >= 100_000 ? s.Duration / 60_000 : Math.Max(1, s.Duration / 60))
            .ToList();

        var dto = new StopAnalyticsDto(
            allStops.Count,
            Math.Round((decimal)durations.Average(), 1),
            durations.Max(),
            isPartial);

        return ApiResponse<StopAnalyticsDto>.SuccessResponse(dto);
    }
}

public record GetFleetUtilizationQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<FleetUtilizationDto>>;

/// <summary>
/// First real consumer of GpsFleetStatusSnapshots beyond the Live Map trend chart. Uses its own,
/// wider range cap (~400 days, enough for a 12-month view) rather than reusing/relaxing
/// GetGpsFleetStatusHistoryQuery's 90-day cap, which exists for Live Map's own UX and shouldn't
/// change on Analytics' behalf.
/// </summary>
public class GetFleetUtilizationQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetFleetUtilizationQuery, ApiResponse<FleetUtilizationDto>>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(400);

    public async Task<ApiResponse<FleetUtilizationDto>> Handle(GetFleetUtilizationQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
            return ApiResponse<FleetUtilizationDto>.FailResponse("'from' must be before 'to'.");

        if (toDate - fromDate > MaxRange)
            return ApiResponse<FleetUtilizationDto>.FailResponse("Date range cannot exceed 400 days.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var snapshots = (await connection.QueryAsync<(DateTime SnapshotAt, int TotalVehicles, int Moving, int Idle, int Parked, int Offline)>(
            new CommandDefinition(
                """
                SELECT SnapshotAt, TotalVehicles, Moving, Idle, Parked, Offline
                FROM GpsFleetStatusSnapshots
                WHERE TenantId = @TenantId AND SnapshotAt BETWEEN @FromDate AND @ToDate
                ORDER BY SnapshotAt ASC
                """,
                new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        if (snapshots.Count == 0)
            return ApiResponse<FleetUtilizationDto>.SuccessResponse(new FleetUtilizationDto(0, 0, 0, 0, 0, "No data", []));

        var runningHours = snapshots.Sum(s => s.Moving);
        var idleHours = snapshots.Sum(s => s.Idle);
        var parkingHours = snapshots.Sum(s => s.Parked);
        var offlineHours = snapshots.Sum(s => s.Offline);
        var totalAvailableHours = snapshots.Sum(s => s.TotalVehicles);

        var utilizationPercent = totalAvailableHours > 0
            ? Math.Round(runningHours * 100m / totalAvailableHours, 1)
            : 0;

        var daily = snapshots
            .GroupBy(s => s.SnapshotAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var totalForDay = g.Sum(s => s.TotalVehicles);
                var pct = totalForDay > 0 ? Math.Round(g.Sum(s => s.Moving) * 100m / totalForDay, 1) : 0;
                return new DailyUtilizationDto(g.Key, pct);
            })
            .ToList();

        var dto = new FleetUtilizationDto(
            runningHours, idleHours, parkingHours, offlineHours, utilizationPercent, LabelFor(utilizationPercent), daily);

        return ApiResponse<FleetUtilizationDto>.SuccessResponse(dto);
    }

    private static string LabelFor(decimal percent) => percent switch
    {
        >= 80 => "Excellent",
        >= 60 => "Good",
        >= 40 => "Fair",
        _ => "Poor"
    };
}

public record GetAnalyticsTrendsQuery(DateTime? FromDate, DateTime? ToDate, string Granularity = "daily")
    : IRequest<ApiResponse<TrendsDto>>;

/// <summary>Rollup-backed (GpsVehicleDailyStats) so long ranges don't scan 90-day-purged GpsPositions or recompute fleet sums from GpsTrips on every request.</summary>
public class GetAnalyticsTrendsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetAnalyticsTrendsQuery, ApiResponse<TrendsDto>>
{
    public async Task<ApiResponse<TrendsDto>> Handle(GetAnalyticsTrendsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = (request.FromDate ?? DateTime.UtcNow.AddMonths(-3)).Date;
        var toDate = (request.ToDate ?? DateTime.UtcNow).Date;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = (await connection.QueryAsync<(DateTime StatDate, decimal DistanceKm, int TripCount, int? OverspeedCount)>(
            new CommandDefinition(
                """
                SELECT StatDate, DistanceKm, TripCount, OverspeedCount
                FROM GpsVehicleDailyStats
                WHERE TenantId = @TenantId AND StatDate BETWEEN @FromDate AND @ToDate
                """,
                new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        DateTime Bucket(DateTime d) => request.Granularity.ToLowerInvariant() switch
        {
            "monthly" => new DateTime(d.Year, d.Month, 1),
            "weekly" => d.AddDays(-(int)d.DayOfWeek),
            _ => d.Date
        };

        var points = rows
            .GroupBy(r => Bucket(r.StatDate))
            .OrderBy(g => g.Key)
            .Select(g => new TrendPointDto(g.Key, g.Sum(r => r.DistanceKm), g.Sum(r => r.TripCount), g.Sum(r => r.OverspeedCount ?? 0)))
            .ToList();

        return ApiResponse<TrendsDto>.SuccessResponse(new TrendsDto(points));
    }
}

public record GetComparativeAnalyticsQuery(
    DateTime FromDateA,
    DateTime ToDateA,
    DateTime? FromDateB,
    DateTime? ToDateB,
    int? BranchId = null,
    int? DepartmentId = null)
    : IRequest<ApiResponse<ComparativeAnalyticsDto>>;

/// <summary>
/// Period-vs-period comparison (e.g. this month vs last month), reusing GetFleetTripSummaryQuery's
/// existing Branch/Department filter pattern for each side. Branch-vs-branch/department-vs-department
/// comparison is achievable today by calling this (or the overview) once per BranchId/DepartmentId
/// from the frontend — a dedicated grouped-by-branch endpoint was scoped out to keep this handler
/// focused on the period-comparison case the spec's examples ("current vs previous month") ask for.
/// </summary>
public class GetComparativeAnalyticsQueryHandler(IMediator mediator)
    : IRequestHandler<GetComparativeAnalyticsQuery, ApiResponse<ComparativeAnalyticsDto>>
{
    public async Task<ApiResponse<ComparativeAnalyticsDto>> Handle(GetComparativeAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var summaryA = await mediator.Send(
            new GetFleetTripSummaryQuery(request.FromDateA, request.ToDateA, request.BranchId, request.DepartmentId, null),
            cancellationToken);

        if (!summaryA.Success || summaryA.Data is null)
            return ApiResponse<ComparativeAnalyticsDto>.FailResponse(summaryA.Message ?? "Failed to load period A.");

        var periodA = new ComparativePeriodDto(
            "Period A", (decimal)summaryA.Data.DistanceKm, summaryA.Data.TripCount, summaryA.Data.AvgSpeedKmh, summaryA.Data.OverspeedCount);

        ComparativePeriodDto? periodB = null;
        if (request.FromDateB.HasValue && request.ToDateB.HasValue)
        {
            var summaryB = await mediator.Send(
                new GetFleetTripSummaryQuery(request.FromDateB, request.ToDateB, request.BranchId, request.DepartmentId, null),
                cancellationToken);

            if (summaryB is { Success: true, Data: not null })
            {
                periodB = new ComparativePeriodDto(
                    "Period B", (decimal)summaryB.Data.DistanceKm, summaryB.Data.TripCount, summaryB.Data.AvgSpeedKmh, summaryB.Data.OverspeedCount);
            }
        }

        return ApiResponse<ComparativeAnalyticsDto>.SuccessResponse(new ComparativeAnalyticsDto(periodA, periodB));
    }
}
