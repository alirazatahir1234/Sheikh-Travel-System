using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Grants OWNER Driver.Create / Update / Delete / Assign / ManageStatus for existing tenants.
/// Template already updated; this syncs RolePermissions rows (idempotent inserts).
/// </summary>
public static class OwnerDriverCrudPermissionsMigration
{
    private static readonly string[] DriverCrudPermissions =
    [
        "Driver.Create",
        "Driver.Update",
        "Driver.Delete",
        "Driver.Assign",
        "Driver.ManageStatus",
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "OWNER",
            DriverCrudPermissions,
            cancellationToken);

        // Keep RolePermissions aligned with the full current Owner template.
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "OWNER",
            TenantRolePermissionTemplates.Owner,
            cancellationToken);

        logger.LogInformation(
            "OwnerDriverCrudPermissionsMigration applied (OWNER driver create/update/delete/assign/manage-status).");
    }
}
