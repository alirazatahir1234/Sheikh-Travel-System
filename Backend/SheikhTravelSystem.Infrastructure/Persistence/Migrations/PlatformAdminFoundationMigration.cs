using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 1 Platform Admin Foundation: ops permissions + synced Super-Admin menus.
/// </summary>
public static class PlatformAdminFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var permissions = new (string Module, string Code, string Desc)[]
        {
            ("Platform", PlatformPermissions.MigrationsView, "View schema migration status"),
            ("Platform", PlatformPermissions.MigrationsManage, "Apply pending schema migrations"),
            ("Platform", PlatformPermissions.SystemReset, "Reset database (Dev/Staging Super Admin only)"),
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

        // Super Admin on platform tenant gets every permission (including new ops codes).
        var codes = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PermissionCode FROM Permissions", cancellationToken: cancellationToken))).ToList();
        await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
            connection, tenantId: 1, "SUPER_ADMIN", codes, cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = 'platform')
            INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
            VALUES (N'Platform', 'platform', 'settings_applications', 9, 1);

            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = 'organization')
            INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
            VALUES (N'Organization', 'organization', 'corporate_fare', 7, 1);

            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = 'access_control')
            INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
            VALUES (N'Access Control', 'access_control', 'admin_panel_settings', 8, 1);
            """, cancellationToken: cancellationToken));

        await UpsertMenuAsync(connection, "organization", "Tenants", "/platform/tenants", "business",
            PlatformPermissions.TenantsView, 0, cancellationToken);
        await UpsertMenuAsync(connection, "organization", "Hierarchy", "/platform/organization-designer", "account_tree",
            PlatformPermissions.BranchesManage, 1, cancellationToken);
        await UpsertMenuAsync(connection, "organization", "Branches", "/platform/branches", "location_city",
            PlatformPermissions.BranchesManage, 2, cancellationToken);
        await UpsertMenuAsync(connection, "organization", "Departments", "/platform/departments", "domain",
            PlatformPermissions.DepartmentsManage, 3, cancellationToken);

        await UpsertMenuAsync(connection, "access_control", "Access Control", "/platform/access-control", "verified_user",
            PlatformPermissions.RolesView, 0, cancellationToken);
        await UpsertMenuAsync(connection, "access_control", "Users", "/users", "manage_accounts",
            PlatformPermissions.UsersView, 1, cancellationToken);
        await UpsertMenuAsync(connection, "access_control", "Roles", "/platform/access-control?tab=roles", "security",
            PlatformPermissions.RolesView, 2, cancellationToken);
        await UpsertMenuAsync(connection, "access_control", "Allowance Rules", "/driver-allowance-rules", "rule",
            PlatformPermissions.RolesManage, 3, cancellationToken);

        await UpsertMenuAsync(connection, "platform", "Platform Home", "/platform", "settings_applications",
            PlatformPermissions.TenantsView, 0, cancellationToken);
        await UpsertMenuAsync(connection, "platform", "Modules", "/platform/module-management", "extension",
            PlatformPermissions.TenantsView, 1, cancellationToken);
        await UpsertMenuAsync(connection, "platform", "Plans", "/platform/subscription-management", "subscriptions",
            PlatformPermissions.TenantsView, 2, cancellationToken);
        await UpsertMenuAsync(connection, "platform", "Migration Manager", "/platform/migrations", "storage",
            PlatformPermissions.MigrationsView, 3, cancellationToken);
        await UpsertMenuAsync(connection, "platform", "Database Reset", "/platform/maintenance", "build_circle",
            PlatformPermissions.SystemReset, 4, cancellationToken);
        await UpsertMenuAsync(connection, "platform", "Settings", "/settings", "tune",
            PlatformPermissions.SettingsView, 5, cancellationToken);
        await UpsertMenuAsync(connection, "platform", "Audit Logs", "/audit-logs", "history",
            PlatformPermissions.AuditLogsView, 6, cancellationToken);

        // Platform owns Settings + Database Reset. Retire legacy Administration copies.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.IsActive = 0
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
            WHERE m.ModuleKey = 'administration'
              AND (
                    pm.Name IN (N'Settings', N'Database Reset', N'Maintenance', N'Tenant Settings', N'System Configuration')
                 OR pm.Route IN (N'/settings', N'/platform/maintenance')
              );
            """, cancellationToken: cancellationToken));

        logger.LogInformation("PlatformAdminFoundationMigration applied (ops permissions + platform menus).");
    }

    private static async Task UpsertMenuAsync(
        System.Data.IDbConnection connection,
        string moduleKey,
        string name,
        string route,
        string icon,
        string permission,
        int sort,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.Route = @Route, pm.Icon = @Icon, pm.PermissionCode = @Permission,
                pm.SortOrder = @Sort, pm.IsActive = 1
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = @ModuleKey
            WHERE pm.Name = @Name;

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive)
            SELECT m.Id, NULL, @Name, @Route, @Icon, @Permission, @Sort, 1
            FROM PlatformModules m
            WHERE m.ModuleKey = @ModuleKey
              AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.ModuleId = m.Id AND x.Name = @Name);
            """, new { ModuleKey = moduleKey, Name = name, Route = route, Icon = icon, Permission = permission, Sort = sort },
            cancellationToken: ct));
    }
}
