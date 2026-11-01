using System.Security.Claims;
using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using Xunit;

namespace SheikhTravelSystem.Tests.Auth;

public class PlatformRoleClaimsTests
{
    [Fact]
    public void IsDriver_true_for_legacy_ClaimTypes_Role_Driver_with_driver_id()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Driver"),
            new Claim("driver_id", "42"),
        ], "test"));

        PlatformRoleClaims.IsDriver(user).Should().BeTrue();
        PlatformRoleClaims.HasRole(user, "DRIVER").Should().BeTrue();
    }

    [Fact]
    public void IsDriver_true_for_RBAC_role_claim_DRIVER()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("role", "DRIVER"),
            new Claim("primary_role", "DRIVER"),
            new Claim("driver_id", "7"),
        ], "test"));

        PlatformRoleClaims.IsDriver(user).Should().BeTrue();
    }

    [Fact]
    public void IsDriver_false_for_fleet_manager()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("role", "FLEET_MANAGER"),
        ], "test"));

        PlatformRoleClaims.IsDriver(user).Should().BeFalse();
    }
}
