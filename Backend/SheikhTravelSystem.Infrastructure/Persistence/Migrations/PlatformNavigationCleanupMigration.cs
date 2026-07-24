using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// One-time Platform Navigation Audit cleanup: remove legacy Administration duplicates
/// that were moved to Platform (Settings, Database Reset) while keeping Notification Center.
/// </summary>
public static class PlatformNavigationCleanupMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus') THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken)) != 1)
            return;

        // Canonical owners: Platform owns Settings + Database Reset.
        await EnsurePlatformMenuAsync(connection, "Settings", "/settings", "tune",
            PlatformPermissions.SettingsView, 5, cancellationToken);
        await EnsurePlatformMenuAsync(connection, "Database Reset", "/platform/maintenance", "build_circle",
            PlatformPermissions.SystemReset, 4, cancellationToken);

        // Retire legacy Administration copies (and related aliases).
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.IsActive = 0, pm.Visible = 0, pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
            WHERE m.ModuleKey = N'administration'
              AND (
                    pm.Name IN (
                        N'Settings', N'Tenant Settings', N'System Configuration',
                        N'Database Reset', N'Maintenance')
                 OR pm.Route IN (N'/settings', N'/platform/maintenance')
              );
            """, cancellationToken: cancellationToken));

        // Administration keeps operational tenant tools only.
        await EnsureAdminMenuAsync(connection, "Notification Center", "/notifications", "notifications",
            PlatformPermissions.DashboardView, 10, cancellationToken);

        // Access Control: Users / Roles / Permissions (canonical identity hub).
        await EnsureAccessMenuAsync(connection, "Users", "/users", "manage_accounts",
            PlatformPermissions.UsersView, 1, cancellationToken);
        await EnsureAccessMenuAsync(connection, "Roles", "/platform/access-control?tab=roles", "security",
            PlatformPermissions.RolesView, 2, cancellationToken);
        await EnsureAccessMenuAsync(connection, "Permissions", "/platform/access-control?tab=permissions", "verified_user",
            PlatformPermissions.RolesView, 3, cancellationToken);

        // Cross-module route duplicates: if Platform owns the route, hide the same route under administration.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE adminMenu SET adminMenu.IsActive = 0, adminMenu.Visible = 0, adminMenu.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus adminMenu
            INNER JOIN PlatformModules adminMod ON adminMod.Id = adminMenu.ModuleId AND adminMod.ModuleKey = N'administration'
            INNER JOIN PlatformMenus platformMenu ON platformMenu.Route = adminMenu.Route AND platformMenu.IsActive = 1
            INNER JOIN PlatformModules platformMod ON platformMod.Id = platformMenu.ModuleId AND platformMod.ModuleKey = N'platform'
            WHERE adminMenu.IsActive = 1;
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "PlatformNavigationCleanupMigration applied (Administration duplicates retired; Platform owns Settings/Database Reset).");
    }

    private static async Task EnsurePlatformMenuAsync(
        System.Data.IDbConnection connection,
        string name, string route, string icon, string permission, int sort,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.Route = @Route, pm.Icon = @Icon, pm.PermissionCode = @Permission,
                pm.SortOrder = @Sort, pm.IsActive = 1, pm.Visible = 1, pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = N'platform'
            WHERE pm.Name = @Name;

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Visible, ModuleKey, UpdatedAt)
            SELECT m.Id, NULL, @Name, @Route, @Icon, @Permission, @Sort, 1,
                   @Name, 1, N'platform', SYSUTCDATETIME()
            FROM PlatformModules m
            WHERE m.ModuleKey = N'platform'
              AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.ModuleId = m.Id AND x.Name = @Name);
            """, new { Name = name, Route = route, Icon = icon, Permission = permission, Sort = sort },
            cancellationToken: ct));
    }

    private static async Task EnsureAdminMenuAsync(
        System.Data.IDbConnection connection,
        string name, string route, string icon, string permission, int sort,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.Route = @Route, pm.Icon = @Icon, pm.PermissionCode = @Permission,
                pm.SortOrder = @Sort, pm.IsActive = 1, pm.Visible = 1, pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = N'administration'
            WHERE pm.Name = @Name;

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Visible, ModuleKey, UpdatedAt)
            SELECT m.Id, NULL, @Name, @Route, @Icon, @Permission, @Sort, 1,
                   @Name, 1, N'administration', SYSUTCDATETIME()
            FROM PlatformModules m
            WHERE m.ModuleKey = N'administration'
              AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.ModuleId = m.Id AND x.Name = @Name);
            """, new { Name = name, Route = route, Icon = icon, Permission = permission, Sort = sort },
            cancellationToken: ct));
    }

    private static async Task EnsureAccessMenuAsync(
        System.Data.IDbConnection connection,
        string name, string route, string icon, string permission, int sort,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.Route = @Route, pm.Icon = @Icon, pm.PermissionCode = @Permission,
                pm.SortOrder = @Sort, pm.IsActive = 1, pm.Visible = 1, pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = N'access_control'
            WHERE pm.Name = @Name;

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Visible, ModuleKey, UpdatedAt)
            SELECT m.Id, NULL, @Name, @Route, @Icon, @Permission, @Sort, 1,
                   @Name, 1, N'access_control', SYSUTCDATETIME()
            FROM PlatformModules m
            WHERE m.ModuleKey = N'access_control'
              AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.ModuleId = m.Id AND x.Name = @Name);
            """, new { Name = name, Route = route, Icon = icon, Permission = permission, Sort = sort },
            cancellationToken: ct));
    }
}
