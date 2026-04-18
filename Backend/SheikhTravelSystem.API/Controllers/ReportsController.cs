using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Reports.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Provides analytical reporting endpoints.
/// </summary>
public class ReportsController : BaseApiController
{
    /// <summary>
    /// Gets booking report data for a date range.
    /// </summary>
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookingReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetBookingReportQuery(fromDate, toDate)));

    /// <summary>
    /// Gets revenue report data for a date range.
    /// </summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetRevenueReportQuery(fromDate, toDate)));

    /// <summary>
    /// Gets vehicle profit report data.
    /// </summary>
    [HttpGet("vehicle-profit")]
    public async Task<IActionResult> GetVehicleProfit([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int? vehicleId)
        => Ok(await Mediator.Send(new GetVehicleProfitQuery(fromDate, toDate, vehicleId)));

    /// <summary>
    /// Gets driver performance report data.
    /// </summary>
    [HttpGet("driver-performance")]
    public async Task<IActionResult> GetDriverPerformance([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetDriverPerformanceQuery(fromDate, toDate)));
}
