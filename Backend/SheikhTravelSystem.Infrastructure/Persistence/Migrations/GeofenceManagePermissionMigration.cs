using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds Gps.GeofenceManage and grants it to OWNER / TENANT_ADMIN / SUPER_ADMIN plus every role that already
/// held Gps.DeviceManage (which previously gated zone writes), so nobody loses access. Idempotent.
/// </summary>
public static class GeofenceManagePermissionMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'Permissions', N'U') IS NOT NULL AND OBJECT_ID(N'RolePermissions', N'U') IS NOT NULL THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken)) != 1)
            return;

        var granted = await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (N'GPS', @Code, N'Create, edit and delete geofence zones, assignments and Entry/Exit rules');

            INSERT INTO RolePermissions (RoleId, PermissionId)
            SELECT DISTINCT r.Id, p.Id
            FROM Roles r
            CROSS JOIN Permissions p
            WHERE p.PermissionCode = @Code
              AND (
                UPPER(r.Code) IN (N'OWNER', N'TENANT_ADMIN', N'SUPER_ADMIN')
                OR EXISTS (
                    SELECT 1 FROM RolePermissions rp
                    INNER JOIN Permissions dp ON dp.Id = rp.PermissionId
                    WHERE rp.RoleId = r.Id AND dp.PermissionCode = @DeviceManage)
              )
              AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);
            """,
            new { Code = GpsPermissions.GeofenceManage, DeviceManage = GpsPermissions.DeviceManage },
            cancellationToken: cancellationToken));

        logger.LogInformation("GeofenceManagePermissionMigration applied ({Rows} row(s) affected).", granted);
    }
}
