using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Company;
using SheikhTravelSystem.Application.Features.Platform;

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
    /// <summary>Visible feature catalog (alias of /catalog).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetFeatureRegistryCatalogQuery()));

    /// <summary>Global feature catalog (metadata only — not a Feature Builder).</summary>
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog()
        => Ok(await Mediator.Send(new GetFeatureRegistryCatalogQuery()));

    /// <summary>Company feature enablement for current tenant (or ?tenantId= for platform admins).</summary>
    [HttpGet("company")]
    public async Task<IActionResult> GetCompanyFeatures([FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetCompanyFeatureRegistryQuery(tenantId)));

    /// <summary>Enabled feature metadata for the current company (tenant).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyCompanyFeatures()
        => Ok(await Mediator.Send(new GetCompanyFeatureRegistryQuery()));

    /// <summary>Feature metadata for a specific company (tenant). Super Admin / tenant access.</summary>
    [RequirePermission(PlatformPermissions.TenantsView)]
    [HttpGet("company/{tenantId:int}")]
    public async Task<IActionResult> GetCompanyFeaturesById(int tenantId)
        => Ok(await Mediator.Send(new GetCompanyFeatureRegistryQuery(tenantId)));

    /// <summary>Single feature registry entry by key.</summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetByKey(string key)
        => Ok(await Mediator.Send(new GetFeatureByKeyQuery(key)));

    /// <summary>Enable/disable company features (configuration only — no flags/rollouts).</summary>
    [RequirePermission(PlatformPermissions.TenantsManage)]
    [HttpPut("company")]
    public async Task<IActionResult> SetCompanyFeatures([FromBody] SetCompanyFeaturesRequest request)
        => Ok(await Mediator.Send(new SetCompanyFeaturesCommand(request.TenantId, request.EnabledFeatureKeys)));
}

public record SetCompanyFeaturesRequest(int TenantId, IReadOnlyList<string> EnabledFeatureKeys);

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
