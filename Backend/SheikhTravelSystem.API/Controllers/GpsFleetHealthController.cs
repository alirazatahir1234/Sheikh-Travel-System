using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Explainable Live Map fleet health under <c>api/gps/fleet-health</c>.
/// Dedicated controller so GpsTrackingController stays untouched for Maps/health work.
/// </summary>
[Authorize]
[RequirePermission(AnalyticsPermissions.GpsView)]
[Route("api/gps")]
public sealed class GpsFleetHealthController : BaseApiController
{
    /// <summary>
    /// Explainable fleet health (Optimal/Healthy/Attention/Critical/Unknown) from real
    /// GPS device, maintenance, insurance, and critical-alert factors — not operational status.
    /// </summary>
    [HttpGet("fleet-health")]
    public async Task<IActionResult> GetFleetHealth()
        => Ok(await Mediator.Send(new GetFleetHealthQuery()));
}
