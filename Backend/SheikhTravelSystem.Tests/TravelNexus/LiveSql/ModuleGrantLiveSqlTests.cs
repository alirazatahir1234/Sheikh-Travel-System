using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;

namespace SheikhTravelSystem.Tests.TravelNexus.LiveSql;

/// <summary>
/// Grant-on-module-enable against a real database: a company provisioned without TravelNexus has no Travel permissions;
/// enabling the module through the real handler grants <see cref="TravelPermissions.All"/> to TENANT_ADMIN and OWNER;
/// disabling keeps them; enabling again and the backfill migration are idempotent.
/// </summary>
[Collection(TravelNexusLiveSqlCollection.Name)]
public class ModuleGrantLiveSqlTests(TravelNexusLiveDbFixture fx)
{
    private const string CountSql = """
        SELECT COUNT(*)
        FROM RolePermissions rp
        INNER JOIN Roles r ON r.Id = rp.RoleId
        INNER JOIN Permissions p ON p.Id = rp.PermissionId
        WHERE r.TenantId = @TenantId AND r.Code = @RoleCode AND p.PermissionCode LIKE N'Travel.%'
        """;

    private async Task<int> TravelGrantsAsync(int tenantId, string roleCode)
    {
        using var db = fx.OpenDb();
        return await db.ExecuteScalarAsync<int>(CountSql, new { TenantId = tenantId, RoleCode = roleCode });
    }

    [TravelLiveSqlFact]
    public async Task Enabling_TravelNexus_grants_admin_roles_once_and_disabling_keeps_them()
    {
        var c = await fx.CreateTenantAsync("e2e-company-c", enableTravel: false);
        (await TravelGrantsAsync(c.TenantId, "TENANT_ADMIN")).Should().Be(0, "no module, no permissions");
        (await TravelGrantsAsync(c.TenantId, "OWNER")).Should().Be(0);

        using var session = fx.Open(c);
        await session.Ok(new SetTenantModulesCommand(c.TenantId, [TravelNexusLiveDbFixture.TravelAgency]));
        (await TravelGrantsAsync(c.TenantId, "TENANT_ADMIN")).Should().Be(TravelPermissions.All.Length);
        (await TravelGrantsAsync(c.TenantId, "OWNER")).Should().Be(TravelPermissions.All.Length);

        await session.Ok(new SetTenantModulesCommand(c.TenantId, []));
        (await TravelGrantsAsync(c.TenantId, "TENANT_ADMIN")).Should().Be(TravelPermissions.All.Length, "disabling never revokes");

        await session.Ok(new SetTenantModulesCommand(c.TenantId, [TravelNexusLiveDbFixture.TravelAgency]));
        (await TravelGrantsAsync(c.TenantId, "TENANT_ADMIN")).Should().Be(TravelPermissions.All.Length, "re-enabling is idempotent");

        // The backfill migration finds nothing left to insert for this company and never duplicates a row.
        await TravelNexusModuleGrantBackfillMigration.ApplyAsync(fx.Factory, NullLogger.Instance);
        (await TravelGrantsAsync(c.TenantId, "TENANT_ADMIN")).Should().Be(TravelPermissions.All.Length);
        (await TravelGrantsAsync(c.TenantId, "OWNER")).Should().Be(TravelPermissions.All.Length);

        // Company B (enabled by the fixture through the same handler) was granted the same way; company C's grant did not touch it.
        (await TravelGrantsAsync(fx.B.TenantId, "TENANT_ADMIN")).Should().Be(TravelPermissions.All.Length);
    }

    [TravelLiveSqlFact]
    public async Task Backfill_grants_a_company_that_had_the_module_before_the_hook_existed()
    {
        var d = await fx.CreateTenantAsync("e2e-company-d", enableTravel: false);
        using (var db = fx.OpenDb())
        {
            // Simulate the pre-Phase-8 state: module row present, no grants.
            await db.ExecuteAsync("""
                INSERT INTO TenantModules (TenantId, ModuleId)
                SELECT @TenantId, m.Id FROM Modules m WHERE m.ModuleCode = @Code;
                """, new { TenantId = d.TenantId, Code = TravelNexusLiveDbFixture.TravelAgency });
        }
        (await TravelGrantsAsync(d.TenantId, "OWNER")).Should().Be(0);

        await TravelNexusModuleGrantBackfillMigration.ApplyAsync(fx.Factory, NullLogger.Instance);

        (await TravelGrantsAsync(d.TenantId, "TENANT_ADMIN")).Should().Be(TravelPermissions.All.Length);
        (await TravelGrantsAsync(d.TenantId, "OWNER")).Should().Be(TravelPermissions.All.Length);
    }
}
