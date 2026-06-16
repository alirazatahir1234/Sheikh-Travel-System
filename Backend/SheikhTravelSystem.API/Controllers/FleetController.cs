using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Fleet;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Read endpoints powering the Fleet hub (dashboard, compliance, inspections, assignments).
/// </summary>
public class FleetController : BaseApiController
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
        => Ok(await Mediator.Send(new GetFleetDashboardQuery()));

    [HttpGet("compliance")]
    public async Task<IActionResult> GetCompliance()
        => Ok(await Mediator.Send(new GetComplianceDocumentsQuery()));

    [HttpGet("inspections")]
    public async Task<IActionResult> GetInspections()
        => Ok(await Mediator.Send(new GetInspectionsQuery()));

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments()
        => Ok(await Mediator.Send(new GetAssignmentsQuery()));
}
