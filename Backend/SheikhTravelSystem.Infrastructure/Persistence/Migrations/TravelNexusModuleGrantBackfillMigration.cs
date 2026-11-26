using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 8: one-time backfill of the grant-on-module-enable rule (<see cref="ModulePermissionGrants"/>).
/// Companies that already have an opt-in module (TravelNexus, Rental) switched on get that module's permissions on
/// their administrator roles, exactly as the hook now does when a module is enabled. Additive and idempotent: it only
/// inserts RolePermissions rows that do not exist and never removes anything.
/// </summary>
public static class TravelNexusModuleGrantBackfillMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var ready = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.TenantModules', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.Modules', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.Permissions', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.RolePermissions', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken));
        if (ready == 0)
        {
            logger.LogInformation("TravelNexusModuleGrantBackfillMigration: platform tables not present yet, nothing to backfill");
            return;
        }

        var inserted = 0;
        foreach (var moduleCode in ModulePermissionGrants.Modules)
        {
            foreach (var roleCode in ModulePermissionGrants.Roles)
            {
                foreach (var permissionCode in ModulePermissionGrants.PermissionsFor(moduleCode))
                {
                    inserted += await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO RolePermissions (RoleId, PermissionId)
                        SELECT r.Id, p.Id
                        FROM TenantModules tm
                        INNER JOIN Modules m ON m.Id = tm.ModuleId AND m.ModuleCode = @ModuleCode
                        INNER JOIN Roles r ON r.TenantId = tm.TenantId AND r.Code = @RoleCode
                        INNER JOIN Permissions p ON p.PermissionCode = @PermissionCode
                        WHERE NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
                        """, new { ModuleCode = moduleCode, RoleCode = roleCode, PermissionCode = permissionCode },
                        cancellationToken: cancellationToken));
                }
            }
        }

        logger.LogInformation("TravelNexusModuleGrantBackfillMigration: {Rows} role permission rows backfilled", inserted);
    }
}
