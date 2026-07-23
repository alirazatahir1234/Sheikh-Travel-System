using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds Ai.* and Notification.* permissions and assigns them to system role templates.
/// </summary>
public static class AccessManagementPermissionsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var permissions = new (string Module, string Code, string Desc)[]
        {
            ("AI", AiPermissions.View, "View AI copilot, digests, and recommendations"),
            ("AI", AiPermissions.Manage, "Manage AI provider config, escalation rules, and datasets"),
            ("AI", AiPermissions.ExecuteWrite, "Execute AI write tools (assign driver, send notification)"),
            ("Notifications", NotificationPermissions.View, "View notification center"),
            ("Notifications", NotificationPermissions.Manage, "Manage notification templates, retention, and send"),
        };

        foreach (var (module, code, desc) in permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (@Module, @Code, @Desc);
                """, new { Module = module, Code = code, Desc = desc }, cancellationToken: cancellationToken));
        }

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN",
            [AiPermissions.View, AiPermissions.Manage, AiPermissions.ExecuteWrite,
             NotificationPermissions.View, NotificationPermissions.Manage],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "FLEET_MANAGER",
            [AiPermissions.View, AiPermissions.Manage, AiPermissions.ExecuteWrite,
             NotificationPermissions.View, NotificationPermissions.Manage],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DRIVER_MANAGER",
            [NotificationPermissions.View],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DISPATCHER",
            [AiPermissions.View, NotificationPermissions.View],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "ACCOUNTANT",
            [NotificationPermissions.View],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DRIVER",
            [NotificationPermissions.View],
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN",
            [PlatformPermissions.SettingsView, PlatformPermissions.SettingsManage],
            cancellationToken);

        logger.LogInformation(
            "AccessManagementPermissionsMigration applied (Ai.* + Notification.* + Settings for TENANT_ADMIN).");
    }
}
