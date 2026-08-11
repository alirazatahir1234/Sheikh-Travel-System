using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds Platform.Branches.View and grants it to roles that register vehicles/drivers.
/// </summary>
public static class BranchesViewPermissionMigration
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
            VALUES (N'Platform', @Code, N'View branches (lookup)');
            """,
            new { Code = PlatformPermissions.BranchesView },
            cancellationToken: cancellationToken));

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "TENANT_ADMIN",
            [PlatformPermissions.BranchesView],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "FLEET_MANAGER",
            [PlatformPermissions.BranchesView],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "DRIVER_MANAGER",
            [PlatformPermissions.BranchesView],
            cancellationToken);

        // Ensure a default branch exists for the primary demo tenant.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Branches')
            AND NOT EXISTS (SELECT 1 FROM Branches WHERE TenantId = 1)
            INSERT INTO Branches (TenantId, BranchCode, Name, Address, City, Country, TimeZone, CurrencyCode, Status, IsGpsEnabled, IsActive, CreatedAt)
            VALUES (1, N'HQ-001', N'Head Office', N'Main branch', N'Karachi', N'Pakistan', N'Asia/Karachi', N'PKR', 1, 1, 1, GETUTCDATE());
            """, cancellationToken: cancellationToken));

        logger.LogInformation("BranchesViewPermissionMigration applied successfully.");
    }
}
