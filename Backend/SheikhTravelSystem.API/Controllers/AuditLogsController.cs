using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.AuditLogs.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : BaseApiController
{
    /// <summary>
    /// Gets paginated audit logs with optional filters.
    /// Admin only.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetAuditLogsQuery query)
        => Ok(await Mediator.Send(query));
}
