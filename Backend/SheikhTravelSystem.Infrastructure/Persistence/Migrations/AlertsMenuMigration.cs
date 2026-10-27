using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the Alerts sidebar PlatformModule + PlatformMenus row.
/// Product module <c>ALERTS</c> existed in Modules/TenantModules after
/// <see cref="ModuleAlertsRestructureMigration"/> but was never registered in
/// PlatformMenus — so menus/me (and the ERP sidebar) omitted it even for SuperAdmin.
/// Route matches ModuleRegistrySeed: /gps-tracking/alerts.
/// </summary>
public static class AlertsMenuMigration
{
    public const string PlatformModuleKey = "alerts";
    public const string MenuRoute = "/gps-tracking/alerts";
    public const string MenuName = "Alerts";

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus'
                ) AND EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformModules'
                ) THEN 1 ELSE 0 END
                """,
                cancellationToken: cancellationToken)) != 1)
        {
            logger.LogInformation("AlertsMenuMigration skipped (PlatformMenus/PlatformModules missing).");
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = @ModuleKey)
            BEGIN
                INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible,
                    DisplayName, Description, Visible)
                VALUES (N'Alerts', @ModuleKey, N'notifications_active', 35, 1,
                    N'Alerts', N'Fleet alert events and rules', 1);
            END

            UPDATE PlatformModules
            SET Name = N'Alerts',
                Icon = N'notifications_active',
                SortOrder = CASE WHEN SortOrder IS NULL OR SortOrder = 0 THEN 35 ELSE SortOrder END,
                IsCollapsible = 1,
                DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), N'Alerts'),
                Description = COALESCE(Description, N'Fleet alert events and rules'),
                Visible = 1
            WHERE ModuleKey = @ModuleKey;
            """,
            new { ModuleKey = PlatformModuleKey },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            -- Move/refresh any existing Alerts nav row onto the alerts module.
            UPDATE pm SET
                pm.ModuleId = mod.Id,
                pm.ParentId = NULL,
                pm.Name = @Name,
                pm.Route = @Route,
                pm.Icon = N'notifications_active',
                pm.PermissionCode = @Perm,
                pm.SortOrder = 10,
                pm.IsActive = 1,
                pm.Visible = 1,
                pm.DisplayName = N'Alerts',
                pm.Description = N'Fleet alert events, acknowledge, and resolve',
                pm.Category = N'Alerts',
                pm.FeatureKey = NULL,
                pm.ModuleKey = @ProductModule,
                pm.IsMobileSupported = 1,
                pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            CROSS JOIN PlatformModules mod
            WHERE mod.ModuleKey = @PlatformModule
              AND (pm.Route = @Route OR pm.Name = @Name);

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (
                ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Description, Category, Visible, FeatureKey, ModuleKey,
                IsMobileSupported, UpdatedAt)
            SELECT mod.Id, NULL, @Name, @Route, N'notifications_active', @Perm, 10, 1,
                   N'Alerts', N'Fleet alert events, acknowledge, and resolve', N'Alerts', 1,
                   NULL, @ProductModule, 1, SYSUTCDATETIME()
            FROM PlatformModules mod
            WHERE mod.ModuleKey = @PlatformModule
              AND NOT EXISTS (
                    SELECT 1 FROM PlatformMenus x
                    WHERE x.Route = @Route OR x.Name = @Name);
            """,
            new
            {
                Name = MenuName,
                Route = MenuRoute,
                Perm = GpsPermissions.AlertView,
                PlatformModule = PlatformModuleKey,
                ProductModule = AlertModuleCatalog.AlertsModuleCode
            },
            cancellationToken: cancellationToken));

        logger.LogInformation(
            "AlertsMenuMigration applied (PlatformModules.alerts → {Route}, Permission={Perm}, ProductModule={Product}).",
            MenuRoute, GpsPermissions.AlertView, AlertModuleCatalog.AlertsModuleCode);
    }
}
