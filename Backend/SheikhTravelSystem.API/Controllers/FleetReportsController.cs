using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Reports.Fleet;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Fleet-wide reports (Trip, Vehicle, Driver, Fuel, Speed, Idle, Stop, Event, Alert, Maintenance) —
/// one self-describing endpoint, see GetFleetReportQueryHandler for per-report-type builders.
/// Excludes Driver — fleet-wide reports aren't appropriate for that role.
/// </summary>
[Authorize(Roles = "Admin,Dispatcher,Accountant")]
[Route("api/fleet-reports")]
public class FleetReportsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetReport([FromQuery] GetFleetReportQuery query)
        => Ok(await Mediator.Send(query));
}
