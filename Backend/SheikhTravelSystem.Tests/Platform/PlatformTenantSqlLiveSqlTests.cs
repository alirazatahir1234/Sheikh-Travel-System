using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories;
using SheikhTravelSystem.Tests.Security.Sg01;

namespace SheikhTravelSystem.Tests.Platform;

/// <summary>
/// Regression: the switchable-tenants, license-summary and subscription-revenue queries filtered on
/// <c>Tenants.IsDeleted</c>, a column the Tenants table does not have ("Invalid column name 'IsDeleted'"),
/// so all three endpoints returned 500 and the header company switcher came back empty.
/// Mocked-repository tests cannot catch a schema mismatch, so these run the real SQL. READ-ONLY.
/// Opt-in like the SG-01 live-SQL tests: set SHEIKHGO_SG01_SQL_CONNECTION to a Development connection
/// string; without it the tests are reported as SKIPPED, never as passed.
/// </summary>
public class PlatformTenantSqlLiveSqlTests
{
    private sealed class LiveFactory(string connectionString) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new SqlConnection(connectionString);
    }

    private static PlatformRepository Repository()
    {
        var cs = Environment.GetEnvironmentVariable(Sg01GpsIngestFactsLiveSqlTests.ConnectionEnvName);
        if (string.IsNullOrWhiteSpace(cs))
            Assert.Fail($"{Sg01GpsIngestFactsLiveSqlTests.ConnectionEnvName} is not set.");
        return new PlatformRepository(new LiveFactory(cs!), Mock.Of<ITenantTimeZoneResolver>());
    }

    [LiveSqlFact]
    public async Task Switchable_tenants_query_executes_against_the_real_schema()
    {
        var rows = await Repository().GetSwitchableTenantsAsync();

        rows.Should().NotBeEmpty("the Development database seeds at least the platform tenant");
    }

    [LiveSqlFact]
    public async Task Tenant_subscription_access_query_executes_against_the_real_schema()
    {
        var access = await Repository().GetTenantSubscriptionAccessAsync(tenantId: 1);

        access.Should().NotBeNull("tenant 1 is the seeded platform tenant");
    }

    [LiveSqlFact]
    public async Task Subscription_revenue_dashboard_query_executes_against_the_real_schema()
    {
        var act = async () => await Repository().LoadSubscriptionRevenueDashboardAsync();

        await act.Should().NotThrowAsync();
    }
}
