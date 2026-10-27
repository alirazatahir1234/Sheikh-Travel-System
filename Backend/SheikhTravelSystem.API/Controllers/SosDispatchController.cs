using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.SosDispatch;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/sos-dispatch")]
public class SosDispatchController : BaseApiController
{
    [RequirePermission(GpsPermissions.AlertView)]
    [HttpGet("events")]
    public async Task<IActionResult> ListEvents([FromQuery] string? status = null, [FromQuery] int limit = 50)
        => Ok(await Mediator.Send(new GetSosEventsQuery(status, limit)));

    [RequirePermission(GpsPermissions.AlertResolve)]
    [HttpPost("events/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id)
        => Ok(await Mediator.Send(new ResolveSosEventCommand(id)));

    [RequirePermission(GpsPermissions.AlertView)]
    [HttpGet("partners")]
    public async Task<IActionResult> ListPartners()
        => Ok(await Mediator.Send(new GetSosRecoveryPartnersQuery()));

    [RequirePermission(GpsPermissions.AlertResolve)]
    [HttpPost("partners")]
    public async Task<IActionResult> CreatePartner([FromBody] UpsertSosRecoveryPartnerRequest body)
        => Ok(await Mediator.Send(new UpsertSosRecoveryPartnerCommand(null, body)));

    [RequirePermission(GpsPermissions.AlertResolve)]
    [HttpPut("partners/{id:int}")]
    public async Task<IActionResult> UpdatePartner(int id, [FromBody] UpsertSosRecoveryPartnerRequest body)
        => Ok(await Mediator.Send(new UpsertSosRecoveryPartnerCommand(id, body)));

    [RequirePermission(GpsPermissions.AlertResolve)]
    [HttpDelete("partners/{id:int}")]
    public async Task<IActionResult> DeletePartner(int id)
        => Ok(await Mediator.Send(new DeleteSosRecoveryPartnerCommand(id)));
}
