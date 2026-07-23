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
    public void BranchScoped_Role_Uses_Assignment_Or_Home()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, homeBranchId: 9, homeDepartmentId: null,
            [new DataScopeResolver.RoleAssignmentInput("FLEET_MANAGER", "Branch", 3, null)]);

        result.IsCompanyWide.Should().BeFalse();
        result.Mode.Should().Be(DataScopeMode.Branch);
        result.BranchIds.Should().Equal(3);
    }

    [Fact]
    public void MultiRole_Unions_Branches()
    {
        var result = DataScopeResolver.Resolve(
            1, 10, 1, null,
            [
                new DataScopeResolver.RoleAssignmentInput("FLEET_MANAGER", "Branch", 3, null),
                new DataScopeResolver.RoleAssignmentInput("DISPATCHER", "Branch", 7, null)
            ]);

        result.Mode.Should().Be(DataScopeMode.Branch);
        result.BranchIds.Should().BeEquivalentTo([3, 7]);
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
            [new DataScopeResolver.RoleAssignmentInput("FLEET_MANAGER", "Branch", 3, null)]);

        var ok = DataScopeSql.TryIntersectOptional(scope, requestedBranchId: 99, null,
            out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Branch");
    }
}
