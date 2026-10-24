using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.SmartOps.Commands;
using SheikhTravelSystem.Application.Features.SmartOps.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/smart-alerts")]
public class SmartAlertsController : BaseApiController
{
    private string Actor =>
        User.FindFirst("userName")?.Value
        ?? User.FindFirst(ClaimTypes.Email)?.Value
        ?? User.FindFirst("userId")?.Value
        ?? "unknown";

    [RequirePermission(SmartOpsPermissions.AlertView)]
    [HttpGet]
    public async Task<IActionResult> GetActive(
        [FromServices] IPlatformScope platformScope,
        [FromQuery] SmartAlertRuleType? ruleType = null,
        [FromQuery] int limit = 50)
        => Ok(await Mediator.Send(new GetSmartAlertsQuery(platformScope.TenantId, ruleType, limit)));

    [RequirePermission(SmartOpsPermissions.AlertView)]
    [HttpGet("new-count")]
    public async Task<IActionResult> GetNewCount([FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new GetSmartAlertNewCountQuery(platformScope.TenantId)));

    /// <summary>Ack/resolve gated by SmartOps.AlertManage (seeded to Admin/Owner/FleetManager/Dispatcher).</summary>
    [RequirePermission(SmartOpsPermissions.AlertManage)]
    [HttpPost("{id:long}/acknowledge")]
    public async Task<IActionResult> Acknowledge(long id, [FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new AcknowledgeSmartAlertCommand(platformScope.TenantId, id, Actor)));

    [RequirePermission(SmartOpsPermissions.AlertManage)]
    [HttpPost("{id:long}/resolve")]
    public async Task<IActionResult> Resolve(long id, [FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new ResolveSmartAlertCommand(platformScope.TenantId, id, Actor)));

    [RequirePermission(SmartOpsPermissions.AlertManage)]
    [HttpPost("{id:long}/dismiss")]
    public async Task<IActionResult> Dismiss(long id, [FromServices] IPlatformScope platformScope)
        => Ok(await Mediator.Send(new DismissSmartAlertCommand(platformScope.TenantId, id, Actor)));
}
