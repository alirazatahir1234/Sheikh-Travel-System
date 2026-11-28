using FluentAssertions;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories;

namespace SheikhTravelSystem.Tests.Platform;

/// <summary>
/// The billing cycle failed on live with "Invalid column name 'SupportEmail'": the lookup read
/// Tenants.SupportEmail (the column is on TenantBranding) and compared the numeric Users.Role to
/// role codes. These pin the query to the real schema.
/// </summary>
public class BillingEmailLookupTests
{
    private static readonly string Sql = PlatformRepository.ResolveBillingEmailSql;

    [Fact]
    public void SupportEmail_IsReadFromTenantBranding_NotTenants()
    {
        Sql.Should().Contain("LEFT JOIN TenantBranding b ON b.TenantId = t.Id");
        Sql.Should().Contain("b.SupportEmail");
        Sql.Should().NotContain("t.SupportEmail");
    }

    [Fact]
    public void Admins_AreFoundThroughUserRoles_NotTheNumericUsersRoleColumn()
    {
        Sql.Should().Contain("INNER JOIN UserRoles ur ON ur.UserId = usr.Id");
        Sql.Should().Contain("r.Code IN (N'OWNER', N'TENANT_ADMIN', N'ADMIN', N'SUPER_ADMIN')");
        Sql.Should().NotMatchRegex(@"(?<![.\w])Role\s+IN\s*\(");
    }

    [Fact]
    public void Lookup_StaysInsideTheCompany()
    {
        Sql.Should().Contain("WHERE t.Id = @TenantId");
        Sql.Should().Contain("usr.TenantId = t.Id");
    }

    [Fact]
    public void BlankAddresses_FallThroughToTheNextSource()
    {
        Sql.Should().Contain("NULLIF(LTRIM(RTRIM(tb.BillingEmail)), N'')");
        Sql.Should().Contain("NULLIF(LTRIM(RTRIM(b.SupportEmail)), N'')");
    }
}
