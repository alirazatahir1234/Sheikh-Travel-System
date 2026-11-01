using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.FatigueDetection;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/fatigue-detection")]
public class FatigueDetectionController : BaseApiController
{
    [RequirePermission(GpsPermissions.AlertView)]
    [HttpGet("events")]
    public async Task<IActionResult> ListEvents([FromQuery] string? status = null, [FromQuery] int limit = 50)
        => Ok(await Mediator.Send(new GetFatigueEventsQuery(status, limit)));

    [RequirePermission(PlatformPermissions.TenantsView)]
    [HttpGet("platform/events")]
    public async Task<IActionResult> ListPlatformEvents(
        [FromQuery] int? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100)
        => Ok(await Mediator.Send(new GetFatigueEventsPlatformQuery(tenantId, status, limit)));

    [RequirePermission(GpsPermissions.AlertView)]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
        => Ok(await Mediator.Send(new GetFatigueSettingsQuery()));

    [RequirePermission(GpsPermissions.AlertResolve)]
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] UpsertFatigueTenantSettingsRequest body)
        => Ok(await Mediator.Send(new UpsertFatigueSettingsCommand(body)));

    /// <summary>
    /// Ingest fatigue signal from dashcam AI, phone prototype, or OBD-enriched webhook.
    /// </summary>
    [RequirePermission(GpsPermissions.AlertResolve)]
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestFatigueSignalRequest body)
        => Ok(await Mediator.Send(new IngestFatigueSignalCommand(body)));

    [RequirePermission(GpsPermissions.AlertResolve)]
    [HttpPost("events/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id)
        => Ok(await Mediator.Send(new ResolveFatigueEventCommand(id)));
}
