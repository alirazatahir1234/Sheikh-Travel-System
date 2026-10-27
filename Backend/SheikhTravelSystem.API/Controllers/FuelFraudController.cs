using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.FuelFraud;
using FinancePermissions = SheikhTravelSystem.Application.Common.FinancePermissions;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/fuel-fraud")]
public class FuelFraudController : BaseApiController
{
    [RequirePermission(FinancePermissions.FuelView)]
    [HttpGet("events")]
    public async Task<IActionResult> ListEvents([FromQuery] string? status = null, [FromQuery] int limit = 50)
        => Ok(await Mediator.Send(new GetFuelFraudEventsQuery(status, limit)));

    [RequirePermission("Platform.Tenants.View")]
    [HttpGet("platform/events")]
    public async Task<IActionResult> ListPlatformEvents(
        [FromQuery] int? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100)
        => Ok(await Mediator.Send(new GetFuelFraudEventsPlatformQuery(tenantId, status, limit)));

    [RequirePermission(FinancePermissions.FuelUpdate)]
    [HttpPost("events/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id)
        => Ok(await Mediator.Send(new ResolveFuelFraudEventCommand(id)));
}
