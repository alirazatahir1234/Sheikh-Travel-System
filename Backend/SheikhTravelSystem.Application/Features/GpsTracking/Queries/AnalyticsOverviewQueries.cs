using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetAnalyticsOverviewQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<AnalyticsOverviewDto>>;

/// <summary>
/// Composition over existing queries — no new aggregation logic beyond the overspeed-today count
/// and the live utilization estimate. Fans out via Task.WhenAll the same way
/// GetFleetTripSummaryQuery/GetTripAnalyticsQuery already do for their own sub-fetches.
/// </summary>
public class GetAnalyticsOverviewQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITenantContext tenantContext)
    : IRequestHandler<GetAnalyticsOverviewQuery, ApiResponse<AnalyticsOverviewDto>>
{
    public async Task<ApiResponse<AnalyticsOverviewDto>> Handle(GetAnalyticsOverviewQuery request, CancellationToken cancellationToken)
    {
        var todayStart = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        var fleetStatusTask = mediator.Send(new GetGpsFleetStatusLocalQuery(), cancellationToken);
        var rangeSummaryTask = mediator.Send(
            new GetFleetTripSummaryQuery(request.FromDate, request.ToDate, request.BranchId, request.DepartmentId, request.DriverId),
            cancellationToken);
        var todaySummaryTask = mediator.Send(
            new GetFleetTripSummaryQuery(todayStart, now, request.BranchId, request.DepartmentId, request.DriverId),
            cancellationToken);
        var geofenceStatsTask = mediator.Send(new GetGeofenceStatsQuery(), cancellationToken);
        var overspeedTodayTask = CountOverspeedTodayAsync(cancellationToken);

        await Task.WhenAll(fleetStatusTask, rangeSummaryTask, todaySummaryTask, geofenceStatsTask, overspeedTodayTask);

        var fleetStatus = await fleetStatusTask;
        var rangeSummary = await rangeSummaryTask;

        if (!fleetStatus.Success || fleetStatus.Data is null)
            return ApiResponse<AnalyticsOverviewDto>.FailResponse(fleetStatus.Message ?? "Failed to load fleet status.");

        if (!rangeSummary.Success || rangeSummary.Data is null)
            return ApiResponse<AnalyticsOverviewDto>.FailResponse(rangeSummary.Message ?? "Failed to load trip summary.");

        var status = fleetStatus.Data;
        var range = rangeSummary.Data;

        var todaySummary = await todaySummaryTask;
        var geofenceStats = await geofenceStatsTask;
        var overspeedToday = await overspeedTodayTask;

        var tripsToday = todaySummary is { Success: true, Data: not null } ? todaySummary.Data.TripCount : 0;
        var stopsToday = todaySummary is { Success: true, Data: not null } ? todaySummary.Data.StopCount : 0;
        var geofenceEntriesToday = geofenceStats is { Success: true, Data: not null } ? geofenceStats.Data.TodayEntries : 0;

        var utilizationPercent = status.TotalVehicles > 0
            ? Math.Round((status.Moving + status.Idle) * 100m / status.TotalVehicles, 1)
            : (decimal?)null;

        var dto = new AnalyticsOverviewDto(
            status.TotalVehicles, status.Online, status.Offline, status.Moving, status.Idle, status.Parked,
            (decimal)range.DistanceKm, range.DrivingMinutes, range.IdleMinutes, range.AvgSpeedKmh, range.MaxSpeedKmh,
            range.FuelLiters, range.EngineHours,
            tripsToday, stopsToday, geofenceEntriesToday, overspeedToday,
            utilizationPercent);

        return ApiResponse<AnalyticsOverviewDto>.SuccessResponse(dto);
    }

    private async Task<int> CountOverspeedTodayAsync(CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.EventType IN ('overspeed', 'speed_exceeded') AND e.IsDeleted = 0
              AND e.Timestamp >= CAST(GETUTCDATE() AS DATE)
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }
}
