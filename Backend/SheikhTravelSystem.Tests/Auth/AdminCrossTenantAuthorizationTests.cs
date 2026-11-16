using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.API.Controllers;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Authentication;
using System.Reflection;
using System.Security.Claims;

namespace SheikhTravelSystem.Tests.Auth;

/// <summary>
/// SG-05 C1 / authorization contracts for admin cross-tenant APIs.
/// Full WebApplicationFactory HTTP suite deferred — no existing WAF harness in this repo.
/// </summary>
public class AdminCrossTenantAuthorizationTests
{
    [Fact]
    public void Companies_endpoint_requires_TenantsView()
    {
        var method = typeof(AdminCrossTenantController).GetMethod(nameof(AdminCrossTenantController.GetCompanies));
        var attr = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attr.Should().NotBeNull();
        attr!.Policy.Should().Be(PlatformPermissions.TenantsView);
    }

    [Fact]
    public void Vehicles_endpoint_requires_TenantsDataRead()
    {
        var method = typeof(AdminCrossTenantController).GetMethod(nameof(AdminCrossTenantController.GetVehicles));
        var attr = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attr.Should().NotBeNull();
        attr!.Policy.Should().Be(PlatformPermissions.TenantsDataRead);
    }

    [Fact]
    public async Task TenantsDataRead_succeeds_only_on_platform_tenant_token()
    {
        var requirement = new PermissionRequirement(PlatformPermissions.TenantsDataRead);
        var handler = new PermissionAuthorizationHandler();

        var wrongTenant = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", PlatformPermissions.TenantsDataRead),
            new Claim("tenant_id", "1001")
        ], "test"));
        var wrongCtx = new AuthorizationHandlerContext([requirement], wrongTenant, null);
        await handler.HandleAsync(wrongCtx);
        wrongCtx.HasSucceeded.Should().BeFalse();

        var platform = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", PlatformPermissions.TenantsDataRead),
            new Claim("tenant_id", PlatformTenant.Id.ToString())
        ], "test"));
        var okCtx = new AuthorizationHandlerContext([requirement], platform, null);
        await handler.HandleAsync(okCtx);
        okCtx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public void CanSwitchTenant_requires_DataRead_and_platform_tenant()
    {
        var ok = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", PlatformPermissions.TenantsDataRead),
            new Claim("tenant_id", PlatformTenant.Id.ToString())
        ], "test"));
        PlatformPermissionClaims.CanSwitchTenant(ok).Should().BeTrue();

        var noPerm = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", PlatformTenant.Id.ToString())
        ], "test"));
        PlatformPermissionClaims.CanSwitchTenant(noPerm).Should().BeFalse();
    }
}
