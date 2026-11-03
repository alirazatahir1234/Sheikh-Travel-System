using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeofenceAccessTests
{
    [Fact]
    public void GeofenceManage_IsRegisteredInPermissionCatalog()
        => GpsPermissions.All.Should().Contain(GpsPermissions.GeofenceManage);

    [Fact]
    public void Owner_TenantAdmin_AndEveryDeviceManageRole_CanManageGeofences()
    {
        TenantRolePermissionTemplates.Owner.Should().Contain(GpsPermissions.GeofenceManage);
        TenantRolePermissionTemplates.TenantAdmin.Should().Contain(GpsPermissions.GeofenceManage);

        foreach (var (role, perms) in TenantRolePermissionTemplates.StandardRoles)
        {
            if (perms.Contains(GpsPermissions.DeviceManage))
                perms.Should().Contain(GpsPermissions.GeofenceManage, $"{role} had DeviceManage");
        }
    }

    [Fact]
    public void GeofenceWriteEndpoints_UseGeofenceManage_AndAreFeatureGated()
    {
        var path = Path.Combine(FindBackendRoot(), "SheikhTravelSystem.API", "Controllers", "GpsTrackingController.cs");
        var src = File.ReadAllText(path);
        foreach (var action in new[] { "CreateGeofence(", "UpdateGeofence(", "DeleteGeofence(", "UpsertGeofenceAssignments(" })
        {
            var i = src.IndexOf("public async Task<IActionResult> " + action, StringComparison.Ordinal);
            i.Should().BeGreaterThan(0);
            var attrs = src[Math.Max(0, i - 250)..i];
            attrs.Should().Contain("GpsPermissions.GeofenceManage", action);
            attrs.Should().Contain("RequireFeature(GpsAddOnFeatureCatalog.Geofencing", action);
        }
    }

    [Fact]
    public void CircleRadius_IsBounded()
    {
        GpsGeoHelper.TryValidateGeofenceGeometry("circle", GpsGeoHelper.MaxCircleRadiusMeters + 1, null, out var err)
            .Should().BeFalse();
        err.Should().Contain("at most");
    }

    [Fact]
    public void Polygon_WithRepeatedPoints_IsRejected()
    {
        const string degenerate = """{"type":"Polygon","coordinates":[[[74.3,31.5],[74.3,31.5],[74.4,31.5],[74.3,31.5]]]}""";
        GpsGeoHelper.TryValidateGeofenceGeometry("polygon", 0, degenerate, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(GpsPermissions.GeofenceManage, true)]
    [InlineData(GpsPermissions.DeviceManage, true)]
    [InlineData("GPS.View", false)]
    public async Task OwnerToken_CreateZonePolicy(string claim, bool expected)
    {
        TenantRolePermissionTemplates.Owner.Should().Contain(GpsPermissions.GeofenceManage);
        TenantRolePermissionTemplates.Owner.Should().NotContain(GpsPermissions.DeviceManage);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("role", "OWNER"), new Claim("permission", claim)], "test"));
        var requirement = new PermissionRequirement(GpsPermissions.GeofenceManage);
        var ctx = new AuthorizationHandlerContext([requirement], user, null);
        await new PermissionAuthorizationHandler().HandleAsync(ctx);
        ctx.HasSucceeded.Should().Be(expected);
    }

    [Fact]
    public void GpsOperator_Template_CanManageGeofences()
        => TenantRolePermissionTemplates.GpsOperator.Should().Contain(GpsPermissions.GeofenceManage);

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SheikhTravelSystem.API", "SheikhTravelSystem.API.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Backend root not found");
    }
}
