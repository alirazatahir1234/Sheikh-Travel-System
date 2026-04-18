using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Reports.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class ReportsController : BaseApiController
{
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookingReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetBookingReportQuery(fromDate, toDate)));

    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetRevenueReportQuery(fromDate, toDate)));

    [HttpGet("vehicle-profit")]
    public async Task<IActionResult> GetVehicleProfit([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int? vehicleId)
        => Ok(await Mediator.Send(new GetVehicleProfitQuery(fromDate, toDate, vehicleId)));

    [HttpGet("driver-performance")]
    public async Task<IActionResult> GetDriverPerformance([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        => Ok(await Mediator.Send(new GetDriverPerformanceQuery(fromDate, toDate)));
}
