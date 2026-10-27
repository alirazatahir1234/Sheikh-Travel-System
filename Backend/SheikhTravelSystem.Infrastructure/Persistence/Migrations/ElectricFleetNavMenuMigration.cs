using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds Electric fleet (/fleet/ev) in PlatformMenus for travel and fleet sidebars.
/// EV UI shipped in fleet-management but was never registered in menus/me.
/// </summary>
public static class ElectricFleetNavMenuMigration
{
    public const string MenuRoute = "/fleet/ev";
    public const string MenuName = "Electric fleet";
    public const string ProductModuleCode = "FLEET";
    public const string PermissionCode = "Vehicle.View";

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
            logger.LogInformation("ElectricFleetNavMenuMigration skipped (PlatformMenus/PlatformModules missing).");
            return;
        }

        await UpsertUnderModuleAsync(
            connection,
            TravelFleetOperationsMenuMigration.PlatformModuleKey,
            sortOrder: 30,
            cancellationToken);

        await UpsertUnderModuleAsync(
            connection,
            "fleet",
            sortOrder: 9,
            cancellationToken);

        logger.LogInformation(
            "ElectricFleetNavMenuMigration applied ({Route} under travel-fleet-operations and fleet).",
            MenuRoute);
    }

    private static async Task UpsertUnderModuleAsync(
        System.Data.IDbConnection connection,
        string platformModuleKey,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET
                pm.ModuleId = mod.Id,
                pm.ParentId = NULL,
                pm.Name = @Name,
                pm.Route = @Route,
                pm.Icon = N'ev_station',
                pm.PermissionCode = @Perm,
                pm.SortOrder = @Sort,
                pm.IsActive = 1,
                pm.Visible = 1,
                pm.DisplayName = N'Electric fleet',
                pm.Description = N'EV battery, charging sites, and range-aware dispatch',
                pm.Category = N'Fleet',
                pm.FeatureKey = NULL,
                pm.ModuleKey = @ProductModule,
                pm.IsMobileSupported = 0,
                pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            CROSS JOIN PlatformModules mod
            WHERE mod.ModuleKey = @PlatformModule
              AND pm.ModuleId = mod.Id
              AND pm.Route = @Route;

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (
                ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Description, Category, Visible, FeatureKey, ModuleKey,
                IsMobileSupported, UpdatedAt)
            SELECT mod.Id, NULL, @Name, @Route, N'ev_station', @Perm, @Sort, 1,
                   N'Electric fleet', N'EV battery, charging sites, and range-aware dispatch', N'Fleet', 1,
                   NULL, @ProductModule, 0, SYSUTCDATETIME()
            FROM PlatformModules mod
            WHERE mod.ModuleKey = @PlatformModule
              AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route AND x.ModuleId = mod.Id);
            """,
            new
            {
                Name = MenuName,
                Route = MenuRoute,
                Perm = PermissionCode,
                Sort = sortOrder,
                ProductModule = ProductModuleCode,
                PlatformModule = platformModuleKey
            },
            cancellationToken: cancellationToken));
    }
}
