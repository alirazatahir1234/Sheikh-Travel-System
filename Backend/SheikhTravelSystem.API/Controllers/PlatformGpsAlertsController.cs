using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>Super Admin cross-tenant GPS alert feed (platform operator only — enforced in query handlers).</summary>
[Authorize]
[RequirePermission(PlatformPermissions.GpsControlView)]
[ApiController]
[Route("api/platform/gps/alerts")]
public class PlatformGpsAlertsController : BaseApiController
{
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int? tenantId,
        [FromQuery] int? vehicleId,
        [FromQuery] bool? unacknowledgedOnly,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? driverId,
        [FromQuery] string? eventType,
        [FromQuery] string? severity,
        [FromQuery] string? status,
        [FromQuery] string? readState,
        [FromQuery] string? datePreset,
        [FromQuery] int? geofenceId)
        => Ok(await Mediator.Send(new GetPlatformGpsAlertEventsQuery(
            tenantId,
            vehicleId,
            unacknowledgedOnly,
            from,
            to,
            driverId,
            eventType,
            severity,
            status,
            readState,
            datePreset,
            geofenceId)));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int? tenantId)
        => Ok(await Mediator.Send(new GetPlatformGpsAlertStatsQuery(tenantId)));
}
