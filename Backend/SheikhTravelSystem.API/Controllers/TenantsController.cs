using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Tenants.Queries;
using SheikhTravelSystem.Application.Features.Tenants;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController : BaseApiController
{
    [AllowAnonymous]
    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding()
        => Ok(await Mediator.Send(new GetTenantBrandingQuery()));

    [RequirePermission(PlatformPermissions.TenantsManage)]
    [HttpPost("provision")]
    public async Task<IActionResult> Provision([FromBody] ProvisionTenantCommand command)
        => Ok(await Mediator.Send(command));

    [RequirePermission(PlatformPermissions.TenantsView)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetTenantsQuery()));

    [RequirePermission(PlatformPermissions.TenantsView)]
    [HttpGet("management-stats")]
    public async Task<IActionResult> GetManagementStats()
        => Ok(await Mediator.Send(new GetTenantManagementStatsQuery()));

    [RequirePermission(PlatformPermissions.TenantsView)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetTenantByIdQuery(id)));

    [RequirePermission(PlatformPermissions.TenantsManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [RequirePermission(PlatformPermissions.TenantsManage)]
    [HttpPut("{id:int}/branding")]
    public async Task<IActionResult> UpdateBranding(int id, [FromBody] UpdateTenantBrandingRequest request)
        => Ok(await Mediator.Send(new UpdateTenantBrandingCommand(
            id,
            request.LogoUrl,
            request.PrimaryColor,
            request.Website,
            request.SupportEmail,
            request.Country,
            request.CurrencyCode,
            request.TimeZone)));

    [RequirePermission(PlatformPermissions.TenantsManage)]
    [HttpPost("{id:int}/reset-admin-password")]
    public async Task<IActionResult> ResetAdminPassword(int id, [FromBody] ResetAdminPasswordRequest request)
        => Ok(await Mediator.Send(new ResetTenantAdminPasswordCommand(id, request.NewPassword)));
}

public record ResetAdminPasswordRequest(string NewPassword);

public record UpdateTenantBrandingRequest(
    string? LogoUrl,
    string? PrimaryColor,
    string? Website,
    string? SupportEmail,
    string? Country,
    string? CurrencyCode,
    string? TimeZone);
