using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.AccidentRisk.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(GpsPermissions.AlertView)]
[Route("api/gps/accident-risk")]
public class AccidentRiskController : BaseApiController
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetAccidentRiskDashboardQuery(from, to)));
}
