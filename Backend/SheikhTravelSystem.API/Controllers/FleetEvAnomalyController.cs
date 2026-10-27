using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Fleet.EvAnomaly;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(FleetPermissions.VehicleView)]
[Route("api/fleet")]
public class FleetEvAnomalyController : BaseApiController
{
    [HttpGet("ev/dashboard")]
    public async Task<IActionResult> GetEvDashboard()
        => Ok(await Mediator.Send(new GetEvFleetDashboardQuery()));

    [HttpGet("ev/charging-stations")]
    public async Task<IActionResult> ListChargingStations()
        => Ok(await Mediator.Send(new ListChargingStationsQuery()));

    [RequirePermission(FleetPermissions.VehicleUpdate)]
    [HttpPost("ev/charging-stations")]
    public async Task<IActionResult> UpsertChargingStation([FromBody] UpsertChargingStationRequest body)
        => Ok(await Mediator.Send(new UpsertChargingStationCommand(body)));

    [HttpGet("anomalies")]
    public async Task<IActionResult> ListAnomalies([FromQuery] bool unreviewedOnly = false, [FromQuery] int? vehicleId = null)
        => Ok(await Mediator.Send(new ListFleetAnomaliesQuery(unreviewedOnly, vehicleId)));

    [RequirePermission(FleetPermissions.VehicleUpdate)]
    [HttpPost("anomalies/{id:long}/review")]
    public async Task<IActionResult> ReviewAnomaly(long id, [FromBody] ReviewFleetAnomalyRequest body)
        => Ok(await Mediator.Send(new ReviewFleetAnomalyCommand(id, body)));

    [HttpGet("documents/tracker")]
    public async Task<IActionResult> GetDocumentTracker()
        => Ok(await Mediator.Send(new GetVehicleDocumentTrackerQuery()));

    [RequirePermission(FleetPermissions.VehicleUpdate)]
    [HttpPost("documents/tracker")]
    public async Task<IActionResult> UpsertDocument([FromBody] UpsertComplianceDocumentRequest body)
        => Ok(await Mediator.Send(new UpsertVehicleComplianceDocumentCommand(body)));
}
