using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.AuditLogs.Queries;

namespace SheikhTravelSystem.API.Controllers;

[RequirePermission(PlatformPermissions.AuditLogsView)]
public class AuditLogsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetAuditLogsQuery query)
        => Ok(await Mediator.Send(query));
}
