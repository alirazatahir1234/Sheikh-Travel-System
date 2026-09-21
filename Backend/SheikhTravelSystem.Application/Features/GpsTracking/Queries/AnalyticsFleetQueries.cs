using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

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
public class GetIdleAnalyticsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetIdleAnalyticsQuery, ApiResponse<IdleAnalyticsDto>>
{
    public Task<ApiResponse<IdleAnalyticsDto>> Handle(GetIdleAnalyticsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetIdleAnalyticsAsync(request, cancellationToken);
}

public record GetStopAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<StopAnalyticsDto>>;

public class GetStopAnalyticsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetStopAnalyticsQuery, ApiResponse<StopAnalyticsDto>>
{
    public Task<ApiResponse<StopAnalyticsDto>> Handle(GetStopAnalyticsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetStopAnalyticsAsync(request, cancellationToken);
}

public record GetFleetUtilizationQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<FleetUtilizationDto>>;

/// <summary>
/// First real consumer of GpsFleetStatusSnapshots beyond the Live Map trend chart. Uses its own,
/// wider range cap (~400 days, enough for a 12-month view) rather than reusing/relaxing
/// GetGpsFleetStatusHistoryQuery's 90-day cap, which exists for Live Map's own UX and shouldn't
/// change on Analytics' behalf.
/// </summary>
public class GetFleetUtilizationQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetFleetUtilizationQuery, ApiResponse<FleetUtilizationDto>>
{
    public Task<ApiResponse<FleetUtilizationDto>> Handle(GetFleetUtilizationQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetFleetUtilizationAsync(request, cancellationToken);
}

public record GetAnalyticsTrendsQuery(DateTime? FromDate, DateTime? ToDate, string Granularity = "daily")
    : IRequest<ApiResponse<TrendsDto>>;

/// <summary>Rollup-backed (GpsVehicleDailyStats) so long ranges don't scan 90-day-purged GpsPositions or recompute fleet sums from GpsTrips on every request.</summary>
public class GetAnalyticsTrendsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetAnalyticsTrendsQuery, ApiResponse<TrendsDto>>
{
    public Task<ApiResponse<TrendsDto>> Handle(GetAnalyticsTrendsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetAnalyticsTrendsAsync(request, cancellationToken);
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
