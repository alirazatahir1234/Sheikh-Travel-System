using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Tests.Auth;

public class DataScopeResolverTests
{
    [Fact]
    public void SuperAdmin_Is_CompanyWide()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, homeBranchId: 5, homeDepartmentId: 2,
            [new DataScopeResolver.RoleAssignmentInput("SUPER_ADMIN", "Company", null, null)]);

        result.IsCompanyWide.Should().BeTrue();
        result.Mode.Should().Be(DataScopeMode.Company);
        result.Source.Should().Be("super_admin");
    }

    [Fact]
    public void TenantAdmin_Is_CompanyWide()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, 5, 2,
            [new DataScopeResolver.RoleAssignmentInput("TENANT_ADMIN", "Company", 5, null)]);

        result.IsCompanyWide.Should().BeTrue();
        result.Source.Should().Be("company_admin");
    }

    [Fact]
    public void MultiRole_FleetOps_Is_CompanyWide()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, 1, null,
            [
                new DataScopeResolver.RoleAssignmentInput("FLEET_MANAGER", "Branch", 3, null),
                new DataScopeResolver.RoleAssignmentInput("DISPATCHER", "Branch", 7, null)
            ]);

        result.IsCompanyWide.Should().BeTrue();
        result.Source.Should().Be("fleet_ops");
    }

    [Fact]
    public void BranchScoped_Role_Uses_Assignment_Or_Home()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, homeBranchId: 9, homeDepartmentId: null,
            [new DataScopeResolver.RoleAssignmentInput("BRANCH_MANAGER", "Branch", 3, null)]);

        result.IsCompanyWide.Should().BeFalse();
        result.Mode.Should().Be(DataScopeMode.Branch);
        result.BranchIds.Should().Equal(3);
    }

    [Fact]
    public void Unscoped_User_PassThrough_CompanyWide()
    {
        var result = DataScopeResolver.Resolve(1, 10, null, null, []);

        result.IsCompanyWide.Should().BeTrue();
        result.Source.Should().Be("default");
    }

    [Fact]
    public void DepartmentMode_Wins_When_Departments_Present()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, 4, 8,
            [new DataScopeResolver.RoleAssignmentInput("ACCOUNTANT", "Assigned", 4, 8)]);

        result.Mode.Should().Be(DataScopeMode.Department);
        result.DepartmentIds.Should().Equal(8);
        result.BranchIds.Should().Contain(4);
    }

    [Fact]
    public void TryIntersect_Rejects_Foreign_Branch()
    {
        var scope = DataScopeResolver.Resolve(
            1, 10, 3, null,
            [new DataScopeResolver.RoleAssignmentInput("BRANCH_MANAGER", "Branch", 3, null)]);

        var ok = DataScopeSql.TryIntersectOptional(scope, requestedBranchId: 99, null,
            out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Branch");
    }

    [Fact]
    public void ApplyVehicleScope_BranchMode_Includes_Unassigned_Branch()
    {
        var scope = DataScopeResolver.Resolve(
            1, 10, 3, null,
            [new DataScopeResolver.RoleAssignmentInput("BRANCH_MANAGER", "Branch", 3, null)]);

        var parameters = new Dapper.DynamicParameters();
        var clauses = new List<string>();
        DataScopeSql.ApplyVehicleScope(parameters, scope, "v", clauses);

        clauses.Should().ContainSingle(c => c.Contains("BranchId IS NULL") && c.Contains("IN @DsBranchIds"));
        parameters.Get<int[]>("DsBranchIds").Should().Equal(3);
    }

    [Fact]
    public void FleetManager_Is_CompanyWide()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, homeBranchId: 9, homeDepartmentId: null,
            [new DataScopeResolver.RoleAssignmentInput("FLEET_MANAGER", "Branch", 3, null)]);

        result.IsCompanyWide.Should().BeTrue();
        result.Mode.Should().Be(DataScopeMode.Company);
        result.Source.Should().Be("fleet_ops");
    }

    [Fact]
    public void BranchManager_Stays_BranchScoped()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, homeBranchId: 9, homeDepartmentId: null,
            [new DataScopeResolver.RoleAssignmentInput("BRANCH_MANAGER", "Branch", 3, null)]);

        result.IsCompanyWide.Should().BeFalse();
        result.Mode.Should().Be(DataScopeMode.Branch);
        result.BranchIds.Should().Equal(3);
    }

    [Fact]
    public void GpsOperator_With_FleetManager_Is_CompanyWide()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, homeBranchId: 9, homeDepartmentId: null,
            [
                new DataScopeResolver.RoleAssignmentInput("FLEET_MANAGER", "Branch", 3, null),
                new DataScopeResolver.RoleAssignmentInput("GPS_OPERATOR", "Company", null, null),
                new DataScopeResolver.RoleAssignmentInput("DISPATCHER", "Branch", 7, null)
            ]);

        result.IsCompanyWide.Should().BeTrue();
        result.Mode.Should().Be(DataScopeMode.Company);
        result.Source.Should().Be("gps_operator");
    }

    [Fact]
    public void CompanyScoped_Role_Ignores_Accidental_BranchClamp()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, 9, null,
            [new DataScopeResolver.RoleAssignmentInput("GPS_OPERATOR", "Company", 3, null)]);

        result.IsCompanyWide.Should().BeTrue();
        result.Source.Should().Be("gps_operator");
    }
}
