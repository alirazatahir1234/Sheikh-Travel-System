using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Re-applies <see cref="TenantRolePermissionTemplates"/> to all system roles.
/// Production often has incomplete RolePermissions because PlatformSchemaMigration already
/// ran before templates grew (GPS alerts, maintenance actions, GPS_OPERATOR, etc.).
/// Idempotent inserts only — does not remove custom grants.
/// </summary>
public static class SystemRolePermissionSyncMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Ensure GPS alert + maintenance action rows exist (safe if prior migration already ran).
        await FleetAlertMaintenancePermissionsMigration.ApplyAsync(dbFactory, logger, cancellationToken);

        var tenantIds = (await connection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT Id FROM Tenants",
                cancellationToken: cancellationToken))).ToList();

        foreach (var tenantId in tenantIds)
        {
            await TenantRolePermissionSeeder.SeedSystemRolePermissionsForTenantAsync(
                connection, tenantId, cancellationToken);
        }

        // SUPER_ADMIN: every permission currently in the catalog.
        var allCodes = (await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT PermissionCode FROM Permissions",
                cancellationToken: cancellationToken))).ToList();

        if (allCodes.Count > 0)
        {
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, "SUPER_ADMIN", allCodes, cancellationToken);
        }

        logger.LogInformation(
            "SystemRolePermissionSyncMigration applied for {TenantCount} tenant(s); SUPER_ADMIN synced to {PermCount} permissions.",
            tenantIds.Count,
            allCodes.Count);
    }
}
