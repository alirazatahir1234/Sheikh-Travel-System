using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds Gps.CommandApprove and grants it to Owner / Fleet Manager / Tenant Admin
/// so tenants can approve-and-dispatch PendingApproval engine commands.
/// </summary>
public static class GpsCommandApprovePermissionMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
            AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
            INSERT INTO Permissions (ModuleName, PermissionCode, Description)
            VALUES (N'Fleet', @Code, N'Approve pending GPS commands (engine cut-off) and dispatch to device');
            """,
            new { Code = GpsPermissions.CommandApprove },
            cancellationToken: cancellationToken));

        string[] roles = ["TENANT_ADMIN", "OWNER", "FLEET_MANAGER"];
        foreach (var role in roles)
        {
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection,
                role,
                [GpsPermissions.CommandApprove],
                cancellationToken);
        }

        logger.LogInformation("GpsCommandApprovePermissionMigration applied successfully.");
    }
}
