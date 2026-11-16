using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Admin;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Platform cross-company read APIs (vehicles / companies).
/// Tenant middleware sets <c>IsCrossTenantBypass</c> when no X-Tenant-Id is sent.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminCrossTenantController : BaseApiController
{
    /// <summary>Companies for the platform filter dropdown.</summary>
    [HttpGet("companies")]
    [RequirePermission(PlatformPermissions.TenantsView)]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetAdminCompaniesQuery(), cancellationToken));

    /// <summary>
    /// Cross-company vehicles. Omit <paramref name="companyId"/> for all companies;
    /// pass it for server-side filter to one company.
    /// </summary>
    [HttpGet("vehicles")]
    [RequirePermission(PlatformPermissions.TenantsDataRead)]
    public async Task<IActionResult> GetVehicles(
        [FromQuery] int? companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(new GetAdminVehiclesQuery(companyId, page, pageSize), cancellationToken));
}
