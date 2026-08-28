using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Backfills GPS Alert + Maintenance action permissions that were added to
/// <see cref="PlatformSchemaMigration"/> after many environments had already marked that
/// migration as applied. Without this, Production stays at ~25 Fleet permissions instead of 37.
/// Idempotent: IF NOT EXISTS inserts + RolePermissions NOT EXISTS guards.
/// </summary>
public static class FleetAlertMaintenancePermissionsMigration
{
    private static readonly (string Module, string Code, string Desc)[] Permissions =
    [
        ("Fleet", GpsPermissions.AlertView, "View GPS alert events"),
        ("Fleet", GpsPermissions.AlertAcknowledge, "Acknowledge GPS alerts"),
        ("Fleet", GpsPermissions.AlertResolve, "Resolve GPS alerts"),
        ("Fleet", GpsPermissions.AlertArchive, "Archive GPS alerts"),
        ("Fleet", GpsPermissions.AlertDelete, "Soft-delete GPS alerts"),
        ("Fleet", MaintenancePermissions.Manage, "Create and manage maintenance schedules and records"),
        ("Fleet", MaintenancePermissions.RequestCreate, "Create maintenance service requests"),
        ("Fleet", MaintenancePermissions.RequestApprove, "Approve or reject maintenance service requests"),
        ("Fleet", MaintenancePermissions.WorkOrderManage, "Manage maintenance work orders"),
        ("Fleet", MaintenancePermissions.WorkshopManage, "Manage workshops"),
        ("Fleet", MaintenancePermissions.VendorManage, "Manage vendors"),
        ("Fleet", MaintenancePermissions.ReportView, "View and export maintenance reports"),
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        foreach (var (module, code, desc) in Permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (@Module, @Code, @Desc);
                """, new { Module = module, Code = code, Desc = desc },
                cancellationToken: cancellationToken));
        }

        var allCodes = Permissions.Select(p => p.Code).ToArray();

        // Super Admin should see and hold every Fleet permission for Access Control UI + auth.
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", allCodes, cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", allCodes, cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "FLEET_MANAGER",
            [
                GpsPermissions.AlertView, GpsPermissions.AlertAcknowledge,
                GpsPermissions.AlertResolve, GpsPermissions.AlertArchive,
                MaintenancePermissions.Manage, MaintenancePermissions.RequestCreate,
                MaintenancePermissions.RequestApprove, MaintenancePermissions.WorkOrderManage,
                MaintenancePermissions.WorkshopManage, MaintenancePermissions.VendorManage,
                MaintenancePermissions.ReportView,
            ],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DRIVER_MANAGER",
            [GpsPermissions.AlertView],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DISPATCHER",
            [GpsPermissions.AlertView, GpsPermissions.AlertAcknowledge],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "GPS_OPERATOR",
            [
                GpsPermissions.AlertView, GpsPermissions.AlertAcknowledge,
                GpsPermissions.AlertResolve, GpsPermissions.AlertArchive,
            ],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "ACCOUNTANT",
            [MaintenancePermissions.ReportView, MaintenancePermissions.View],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DRIVER",
            [
                GpsPermissions.AlertView, GpsPermissions.AlertAcknowledge,
                MaintenancePermissions.RequestCreate, MaintenancePermissions.View,
            ],
            cancellationToken);

        logger.LogInformation(
            "FleetAlertMaintenancePermissionsMigration applied ({Count} GPS alert + maintenance permissions).",
            Permissions.Length);
    }
}
