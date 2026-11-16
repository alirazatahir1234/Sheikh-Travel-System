using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>Platform tenant switcher APIs (SG-11).</summary>
[Authorize]
[ApiController]
[Route("api/platform/tenants")]
public class PlatformTenantsController : BaseApiController
{
    /// <summary>
    /// Every tenant the caller may act in, including Suspended / Expired / Deactivated.
    /// </summary>
    [HttpGet("switchable")]
    [RequirePermission(PlatformPermissions.TenantsView)]
    public async Task<IActionResult> GetSwitchable(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetSwitchableTenantsQuery(), cancellationToken));
}
