using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds Analytics → Driver Performance (and companion Fleet Analytics / Client Tracking Links)
/// into PlatformMenus. The ERP page and static menu-config already existed, but menus/me never
/// returned the items — so SuperAdmin and fleet roles never saw them in the sidebar.
/// Permission matches Fleet Tracking (GPS.View); Driver.ViewPerformance gates APIs only.
/// </summary>
public static class DriverPerformanceNavMenuMigration
{
    public const string AnalyticsModuleKey = "analytics";
    public const string ProductModuleCode = "GPS";
    public const string FeatureKey = "gps-tracking";
    public const string PermissionCode = AnalyticsPermissions.GpsView;

    private static readonly (string Name, string Route, string Icon, string DisplayName, string Description, int Sort)[] Items =
    [
        ("Fleet Analytics", "/gps-tracking/analytics", "analytics", "Fleet Analytics",
            "GPS fleet utilization and cost analytics", 5),
        ("Driver Performance", "/gps-tracking/driver-performance", "speed", "Driver Performance",
            "Driver safety, fuel, and on-time scoring", 6),
        ("Client Tracking Links", "/gps-tracking/client-tracking-links", "link", "Client Tracking Links",
            "Shareable live tracking links for clients", 7),
    ];

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
            logger.LogInformation("DriverPerformanceNavMenuMigration skipped (PlatformMenus/PlatformModules missing).");
            return;
        }

        foreach (var item in Items)
            await UpsertAnalyticsItemAsync(connection, item, cancellationToken);

        logger.LogInformation(
            "DriverPerformanceNavMenuMigration applied ({Count} Analytics GPS items, Permission={Perm}).",
            Items.Length, PermissionCode);
    }

    private static async Task UpsertAnalyticsItemAsync(
        System.Data.IDbConnection connection,
        (string Name, string Route, string Icon, string DisplayName, string Description, int Sort) item,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET
                pm.ModuleId = mod.Id,
                pm.ParentId = NULL,
                pm.Name = @Name,
                pm.Route = @Route,
                pm.Icon = @Icon,
                pm.PermissionCode = @Perm,
                pm.SortOrder = @Sort,
                pm.IsActive = 1,
                pm.Visible = 1,
                pm.DisplayName = @DisplayName,
                pm.Description = @Description,
                pm.Category = N'Analytics',
                pm.FeatureKey = @FeatureKey,
                pm.ModuleKey = @ProductModule,
                pm.IsMobileSupported = 0,
                pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            CROSS JOIN PlatformModules mod
            WHERE mod.ModuleKey = @PlatformModule
              AND (pm.Route = @Route OR (pm.Name = @Name AND pm.ModuleId = mod.Id));

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (
                ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Description, Category, Visible, FeatureKey, ModuleKey,
                IsMobileSupported, UpdatedAt)
            SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                   @DisplayName, @Description, N'Analytics', 1,
                   @FeatureKey, @ProductModule, 0, SYSUTCDATETIME()
            FROM PlatformModules mod
            WHERE mod.ModuleKey = @PlatformModule
              AND NOT EXISTS (
                    SELECT 1 FROM PlatformMenus x
                    WHERE x.Route = @Route OR (x.Name = @Name AND x.ModuleId = mod.Id));
            """,
            new
            {
                item.Name,
                item.Route,
                item.Icon,
                item.DisplayName,
                item.Description,
                item.Sort,
                Perm = PermissionCode,
                FeatureKey,
                ProductModule = ProductModuleCode,
                PlatformModule = AnalyticsModuleKey
            },
            cancellationToken: cancellationToken));
    }
}
