using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Company;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Company-facing read APIs. Persistence remains Tenants; Company is the product language.
/// </summary>
[Authorize]
[ApiController]
[Route("api/platform/company")]
public class CompanyController : BaseApiController
{
    /// <summary>Current-tenant company context for ERP summary and mobile session.</summary>
    [HttpGet("context")]
    public async Task<IActionResult> GetContext()
        => Ok(await Mediator.Send(new GetCompanyContextQuery()));
}

[Authorize]
[ApiController]
[Route("api/platform/features")]
public class FeatureRegistryController : BaseApiController
{
    /// <summary>Global feature catalog (metadata only — not a Feature Builder).</summary>
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog()
        => Ok(await Mediator.Send(new GetFeatureCatalogQuery()));

    /// <summary>Enabled feature metadata for the current company (tenant).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyCompanyFeatures()
        => Ok(await Mediator.Send(new GetCompanyFeaturesQuery()));

    /// <summary>Feature metadata for a specific company (tenant). Super Admin / tenant access.</summary>
    [RequirePermission(PlatformPermissions.TenantsView)]
    [HttpGet("company/{tenantId:int}")]
    public async Task<IActionResult> GetCompanyFeatures(int tenantId)
        => Ok(await Mediator.Send(new GetCompanyFeaturesQuery(tenantId)));
}

[Authorize]
[ApiController]
[Route("api/tenants/me")]
public class TenantCompanyAliasController : BaseApiController
{
    /// <summary>Alias: company context for the authenticated tenant.</summary>
    [HttpGet("company-context")]
    public async Task<IActionResult> GetCompanyContext()
        => Ok(await Mediator.Send(new GetCompanyContextQuery()));
}
