using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds DRIVER_MANAGER permission template and adds Trip.View to FLEET_MANAGER for existing tenants.
/// </summary>
public static class DriverManagerRoleTemplateMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Roles')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Tenants')
            BEGIN
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                SELECT t.Id, N'Driver Manager', N'DRIVER_MANAGER', 1, 1, GETUTCDATE()
                FROM Tenants t
                WHERE NOT EXISTS (
                    SELECT 1 FROM Roles r
                    WHERE r.TenantId = t.Id AND r.Code = N'DRIVER_MANAGER'
                );
            END
            """, cancellationToken: cancellationToken));

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "DRIVER_MANAGER",
            TenantRolePermissionTemplates.DriverManager,
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "FLEET_MANAGER",
            ["Trip.View"],
            cancellationToken);

        logger.LogInformation(
            "DriverManagerRoleTemplateMigration applied (DRIVER_MANAGER template + FLEET_MANAGER Trip.View).");
    }
}
