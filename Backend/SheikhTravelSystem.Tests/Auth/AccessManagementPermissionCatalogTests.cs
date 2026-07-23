using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Domain.Entities;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Authentication;
using System.Security.Claims;

namespace SheikhTravelSystem.Tests.Auth;

public class AccessManagementPermissionCatalogTests
{
    [Fact]
    public void Permission_const_classes_cover_seeded_operations_finance_analytics_ai_notifications()
    {
        var catalog = PlatformPermissions.All
            .Concat(FleetPermissions.All)
            .Concat(DriverPermissions.All)
            .Concat(MaintenancePermissions.All)
            .Concat(GpsPermissions.All)
            .Concat(OperationsPermissions.All)
            .Concat(FinancePermissions.All)
            .Concat(AnalyticsPermissions.All)
            .Concat(AiPermissions.All)
            .Concat(NotificationPermissions.All)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        catalog.Should().Contain(OperationsPermissions.BookingView);
        catalog.Should().Contain(OperationsPermissions.BookingUpdate);
        catalog.Should().Contain(OperationsPermissions.BookingDelete);
        catalog.Should().Contain(OperationsPermissions.TripView);
        catalog.Should().Contain(OperationsPermissions.TripCreate);
        catalog.Should().Contain(OperationsPermissions.TripAssign);
        catalog.Should().Contain(OperationsPermissions.RouteCreate);
        catalog.Should().Contain(FinancePermissions.PaymentView);
        catalog.Should().Contain(FinancePermissions.PaymentCreate);
        catalog.Should().Contain(FinancePermissions.FuelView);
        catalog.Should().Contain(FinancePermissions.FuelCreate);
        catalog.Should().Contain(AnalyticsPermissions.ReportView);
        catalog.Should().Contain(AnalyticsPermissions.GpsView);
        catalog.Should().Contain(AnalyticsPermissions.CustomerView);
        catalog.Should().Contain(AnalyticsPermissions.CustomerCreate);
        catalog.Should().Contain(AiPermissions.View);
        catalog.Should().Contain(AiPermissions.Manage);
        catalog.Should().Contain(AiPermissions.ExecuteWrite);
        catalog.Should().Contain(NotificationPermissions.View);
        catalog.Should().Contain(NotificationPermissions.Manage);
        catalog.Should().Contain(DriverPermissions.DriverManage);
        catalog.Count.Should().BeGreaterThanOrEqualTo(60);
    }

    [Fact]
    public void AddPermissionPolicies_registers_every_catalog_code()
    {
        var options = new AuthorizationOptions();
        PermissionPolicyRegistration.AddPermissionPolicies(options);

        foreach (var code in PlatformPermissions.All
                     .Concat(FleetPermissions.All)
                     .Concat(DriverPermissions.All)
                     .Concat(MaintenancePermissions.All)
                     .Concat(GpsPermissions.All)
                     .Concat(OperationsPermissions.All)
                     .Concat(FinancePermissions.All)
                     .Concat(AnalyticsPermissions.All)
                     .Concat(AiPermissions.All)
                     .Concat(NotificationPermissions.All))
        {
            options.GetPolicy(code).Should().NotBeNull($"policy missing for {code}");
        }
    }

    [Fact]
    public void Role_templates_match_expected_matrix()
    {
        TenantRolePermissionTemplates.Dispatcher.Should().Contain(OperationsPermissions.BookingView);
        TenantRolePermissionTemplates.Dispatcher.Should().Contain(OperationsPermissions.BookingUpdate);
        TenantRolePermissionTemplates.Dispatcher.Should().Contain(OperationsPermissions.TripCreate);
        TenantRolePermissionTemplates.Dispatcher.Should().Contain(AiPermissions.View);
        TenantRolePermissionTemplates.Dispatcher.Should().NotContain(AiPermissions.ExecuteWrite);
        TenantRolePermissionTemplates.Dispatcher.Should().NotContain(FinancePermissions.PaymentView);

        TenantRolePermissionTemplates.Accountant.Should().Contain(FinancePermissions.PaymentView);
        TenantRolePermissionTemplates.Accountant.Should().Contain(FinancePermissions.PaymentCreate);
        TenantRolePermissionTemplates.Accountant.Should().Contain(FinancePermissions.FuelUpdate);
        TenantRolePermissionTemplates.Accountant.Should().Contain(AnalyticsPermissions.ReportView);
        TenantRolePermissionTemplates.Accountant.Should().NotContain(OperationsPermissions.BookingCreate);
        TenantRolePermissionTemplates.Accountant.Should().NotContain(GpsPermissions.CommandEngineCutoff);

        TenantRolePermissionTemplates.Driver.Should().Contain(OperationsPermissions.TripView);
        TenantRolePermissionTemplates.Driver.Should().Contain(AnalyticsPermissions.GpsView);
        TenantRolePermissionTemplates.Driver.Should().NotContain(OperationsPermissions.BookingCreate);

        TenantRolePermissionTemplates.FleetManager.Should().Contain(AiPermissions.ExecuteWrite);
        TenantRolePermissionTemplates.TenantAdmin.Should().Contain(PlatformPermissions.SettingsView);
        TenantRolePermissionTemplates.TenantAdmin.Should().Contain(AiPermissions.Manage);
    }
}

public class PermissionAuthorizationHandlerTests
{
    private static async Task<AuthorizationHandlerContext> EvaluateAsync(
        ClaimsPrincipal user,
        string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        var handler = new PermissionAuthorizationHandler();
        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Succeeds_when_permission_claim_present()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", OperationsPermissions.BookingView)
        ], "test"));

        var ctx = await EvaluateAsync(user, OperationsPermissions.BookingView);
        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_when_permission_missing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", FinancePermissions.PaymentView)
        ], "test"));

        var ctx = await EvaluateAsync(user, OperationsPermissions.BookingCreate);
        ctx.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task SuperAdmin_bypasses_all_permissions()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("role", PlatformRoles.SuperAdmin)
        ], "test"));

        var ctx = await EvaluateAsync(user, GpsPermissions.CommandEngineCutoff);
        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task TenantAdmin_bypasses_non_tenant_platform_permissions()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("role", PlatformRoles.TenantAdmin)
        ], "test"));

        var ctx = await EvaluateAsync(user, PlatformPermissions.SettingsView);
        ctx.HasSucceeded.Should().BeTrue();

        var tenants = await EvaluateAsync(user, PlatformPermissions.TenantsManage);
        tenants.HasSucceeded.Should().BeFalse();
    }
}

public class JwtLegacyAdminBridgeTests
{
    [Fact]
    public void GenerateAccessToken_maps_legacy_Admin_to_TENANT_ADMIN_and_permissions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "unit-test-secret-key-at-least-32-chars!!",
                ["JwtSettings:Issuer"] = "test",
                ["JwtSettings:Audience"] = "test",
                ["JwtSettings:ExpiryMinutes"] = "60"
            })
            .Build();

        var jwt = new JwtTokenService(config);
        var user = new User
        {
            Id = 1,
            TenantId = 1,
            Email = "admin@test.com",
            FullName = "Admin",
            Role = UserRole.Admin,
            PasswordHash = "x",
            IsActive = true
        };

        var access = new Application.Common.Interfaces.UserAccessContext(
            1, 1,
            [PlatformRoles.TenantAdmin],
            TenantRolePermissionTemplates.TenantAdmin);

        var token = jwt.GenerateAccessToken(user, access: access);
        token.Should().NotBeNullOrWhiteSpace();

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);
        parsed.Claims.Should().Contain(c => c.Type == "role" && c.Value == PlatformRoles.TenantAdmin);
        parsed.Claims.Should().Contain(c => c.Type == "permission" && c.Value == OperationsPermissions.BookingView);
        parsed.Claims.Should().Contain(c => c.Type == "permission" && c.Value == AiPermissions.View);
    }

    [Fact]
    public void GenerateDriverAccessToken_includes_DRIVER_permissions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "unit-test-secret-key-at-least-32-chars!!",
                ["JwtSettings:Issuer"] = "test",
                ["JwtSettings:Audience"] = "test",
                ["JwtSettings:ExpiryMinutes"] = "480"
            })
            .Build();

        var jwt = new JwtTokenService(config);
        var token = jwt.GenerateDriverAccessToken(10, 20, 1, "Driver", "03001234567");
        var parsed = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);

        parsed.Claims.Should().Contain(c => c.Type == "role" && c.Value == "DRIVER");
        parsed.Claims.Should().Contain(c => c.Type == "permission" && c.Value == OperationsPermissions.TripView);
        parsed.Claims.Should().Contain(c => c.Type == "permission" && c.Value == NotificationPermissions.View);
    }
}
