using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;
using Xunit;

namespace SheikhTravelSystem.Tests.Auth;

public class RoleCodePriorityTests
{
    [Fact]
    public void SelectPrimary_Prefers_SystemAdmin_Over_Lower_Id_Style_Roles()
    {
        // Simulates multi-role users where Role.Id order would have picked DRIVER/ACCOUNTANT first.
        var primary = RoleCodePriority.SelectPrimary(
            ["DRIVER", "ACCOUNTANT", "FLEET_MANAGER", "TENANT_ADMIN"]);

        primary.Should().Be("TENANT_ADMIN");
    }

    [Fact]
    public void SelectPrimary_Prefers_FleetManager_Over_Driver()
    {
        RoleCodePriority.SelectPrimary(["DRIVER", "FLEET_MANAGER"])
            .Should().Be("FLEET_MANAGER");
    }

    [Fact]
    public void SelectPrimary_Prefers_SuperAdmin_First()
    {
        RoleCodePriority.SelectPrimary(["OWNER", "FLEET_MANAGER", "SUPER_ADMIN"])
            .Should().Be("SUPER_ADMIN");
    }

    [Fact]
    public void OrderByPrivilege_Highest_First()
    {
        RoleCodePriority.OrderByPrivilege(["DRIVER", "TENANT_ADMIN", "FLEET_MANAGER"])
            .Should().Equal("TENANT_ADMIN", "FLEET_MANAGER", "DRIVER");
    }

    [Fact]
    public void PreferElevatedRoleCode_Ignores_Low_Privilege_Profile_From_Id_Order()
    {
        WorkspaceBuilderQueries.PreferElevatedRoleCode(
                "DRIVER",
                ["DRIVER", "FLEET_MANAGER", "TENANT_ADMIN"])
            .Should().Be("TENANT_ADMIN");
    }
}
