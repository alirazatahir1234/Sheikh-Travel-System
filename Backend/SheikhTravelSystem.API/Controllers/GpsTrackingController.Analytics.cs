using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Analytics & Business Intelligence routes — all under api/gps/analytics/*. Split from
/// GpsTrackingController.cs since this domain adds ~18 endpoints on top of an already
/// multi-resource controller (see the class doc-comment on the main file).
/// </summary>
public partial class GpsTrackingController
{
    [HttpGet("analytics/overview")]
    public async Task<IActionResult> GetAnalyticsOverview(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId)
        => Ok(await Mediator.Send(new GetAnalyticsOverviewQuery(from, to, branchId, departmentId, driverId)));

    [HttpGet("analytics/distance")]
    public async Task<IActionResult> GetDistanceAnalytics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId)
        => Ok(await Mediator.Send(new GetDistanceAnalyticsQuery(from, to, branchId, departmentId, driverId)));

    [HttpGet("analytics/speed")]
    public async Task<IActionResult> GetSpeedAnalytics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId)
        => Ok(await Mediator.Send(new GetSpeedAnalyticsQuery(from, to, branchId, departmentId, driverId)));

    [HttpGet("analytics/idle")]
    public async Task<IActionResult> GetIdleAnalytics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId)
        => Ok(await Mediator.Send(new GetIdleAnalyticsQuery(from, to, branchId, departmentId, driverId)));

    [HttpGet("analytics/stops")]
    public async Task<IActionResult> GetStopAnalytics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId)
        => Ok(await Mediator.Send(new GetStopAnalyticsQuery(from, to, branchId, departmentId, driverId)));

    [HttpGet("analytics/drivers/ranking")]
    public async Task<IActionResult> GetDriverScoreRanking(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId)
        => Ok(await Mediator.Send(new GetDriverScoreRankingQuery(from, to, branchId, departmentId)));

    [HttpGet("analytics/utilization")]
    public async Task<IActionResult> GetFleetUtilization(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetFleetUtilizationQuery(from, to)));

    [HttpGet("analytics/fuel")]
    public async Task<IActionResult> GetFuelAnalytics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId)
        => Ok(await Mediator.Send(new GetFuelAnalyticsQuery(from, to, branchId, departmentId, driverId)));

    [HttpGet("analytics/geofences")]
    public async Task<IActionResult> GetGeofenceAnalytics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetGeofenceAnalyticsQuery(from, to)));

    [HttpGet("analytics/events")]
    public async Task<IActionResult> GetAlertEventStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? vehicleId)
        => Ok(await Mediator.Send(new GetAlertEventStatsQuery(from, to, vehicleId)));

    [HttpGet("analytics/heatmap")]
    public async Task<IActionResult> GetPositionHeatmap(
        [FromQuery] int[] vehicleIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetPositionHeatmapQuery(vehicleIds, from, to)));

    [HttpGet("analytics/vehicle-health")]
    public async Task<IActionResult> GetVehicleHealth(
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId)
        => Ok(await Mediator.Send(new GetVehicleHealthScoreQuery(branchId, departmentId)));

    [HttpGet("analytics/vehicles/ranking")]
    public async Task<IActionResult> GetVehicleRanking(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId)
        => Ok(await Mediator.Send(new GetVehicleRankingQuery(from, to, branchId, departmentId)));

    [HttpGet("analytics/cost")]
    public async Task<IActionResult> GetCostAnalytics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId)
        => Ok(await Mediator.Send(new GetCostAnalyticsQuery(from, to, branchId, departmentId)));

    [HttpGet("analytics/trends")]
    public async Task<IActionResult> GetAnalyticsTrends(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string granularity = "daily")
        => Ok(await Mediator.Send(new GetAnalyticsTrendsQuery(from, to, granularity)));

    [HttpGet("analytics/comparative")]
    public async Task<IActionResult> GetComparativeAnalytics(
        [FromQuery] DateTime fromA,
        [FromQuery] DateTime toA,
        [FromQuery] DateTime? fromB,
        [FromQuery] DateTime? toB,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId)
        => Ok(await Mediator.Send(new GetComparativeAnalyticsQuery(fromA, toA, fromB, toB, branchId, departmentId)));

    [HttpGet("analytics/reports/schedules")]
    public async Task<IActionResult> GetAnalyticsReportSchedules()
        => Ok(await Mediator.Send(new ListAnalyticsReportSchedulesQuery()));

    [HttpPost("analytics/reports/schedules")]
    public async Task<IActionResult> CreateAnalyticsReportSchedule([FromBody] CreateAnalyticsReportScheduleDto body)
        => Ok(await Mediator.Send(new CreateAnalyticsReportScheduleCommand(body)));

    [HttpPut("analytics/reports/schedules/{id:int}")]
    public async Task<IActionResult> UpdateAnalyticsReportSchedule(int id, [FromBody] UpdateAnalyticsReportScheduleDto body)
        => Ok(await Mediator.Send(new UpdateAnalyticsReportScheduleCommand(id, body)));

    [HttpDelete("analytics/reports/schedules/{id:int}")]
    public async Task<IActionResult> DeleteAnalyticsReportSchedule(int id)
        => Ok(await Mediator.Send(new DeleteAnalyticsReportScheduleCommand(id)));
}
