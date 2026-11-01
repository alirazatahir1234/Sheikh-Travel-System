using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Revokes Gps.Command* from all non-SUPER_ADMIN roles so device Commands are
/// platform Super Admin only (UI already gates on SUPER_ADMIN).
/// Idempotent — safe to re-run.
/// </summary>
public static class GpsCommandsSuperAdminOnlyMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var removed = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE rp
            FROM RolePermissions rp
            INNER JOIN Roles r ON r.Id = rp.RoleId
            INNER JOIN Permissions p ON p.Id = rp.PermissionId
            WHERE UPPER(r.Code) <> N'SUPER_ADMIN'
              AND p.PermissionCode IN @Codes;
            """,
            new { Codes = GpsPermissions.CommandAll },
            cancellationToken: cancellationToken));

        // Keep SUPER_ADMIN RolePermissions aligned with the catalog (includes Command*).
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "SUPER_ADMIN",
            GpsPermissions.CommandAll,
            cancellationToken);

        logger.LogInformation(
            "GpsCommandsSuperAdminOnlyMigration applied; removed {Removed} RolePermission row(s) from non-SUPER_ADMIN roles.",
            removed);
    }
}
