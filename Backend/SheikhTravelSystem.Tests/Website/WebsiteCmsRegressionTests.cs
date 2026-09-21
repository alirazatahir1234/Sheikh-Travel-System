using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Tests.Website;

public class WebsiteCmsRegressionTests
{
    [Fact]
    public void AddPermissionPolicies_registers_all_Website_permissions()
    {
        var options = new AuthorizationOptions();
        PermissionPolicyRegistration.AddPermissionPolicies(options);

        foreach (var code in WebsitePermissions.All)
            options.GetPolicy(code).Should().NotBeNull($"missing policy for {code}");
    }

    [Fact]
    public void PublicFeaturesSql_excludes_draft_and_inactive()
    {
        WebsitePublicSql.PublishedFeatures.Should().Contain("Status = N'Published'");
        WebsitePublicSql.PublishedFeatures.Should().Contain("IsActive = 1");
        WebsitePublicSql.PublishedFeatures.Should().NotContain("Draft");
    }

    [Fact]
    public void PublicPageAndSectionSql_require_published()
    {
        WebsitePublicSql.PageSections.Should().Contain("Status = N'Published'");
        WebsitePublicSql.PageSections.Should().Contain("IsActive = 1");
    }
}
