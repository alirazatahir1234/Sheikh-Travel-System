using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Nests Trips and Routes under a collapsible PlatformModules parent
/// <c>travel-fleet-operations</c> (display name Fleet Operations). Bookings stay under Operations.
/// </summary>
public static class TravelFleetOperationsMenuMigration
{
    public const string PlatformModuleKey = "travel-fleet-operations";

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
            logger.LogInformation("TravelFleetOperationsMenuMigration skipped (PlatformMenus/PlatformModules missing).");
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = @ModuleKey)
            BEGIN
                INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible,
                    DisplayName, Description, Visible)
                VALUES (N'Fleet Operations', @ModuleKey, N'route', 22, 1,
                    N'Fleet Operations', N'Trip lifecycle and route catalog', 1);
            END

            UPDATE PlatformModules
            SET Name = N'Fleet Operations',
                Icon = N'route',
                SortOrder = CASE WHEN SortOrder IS NULL OR SortOrder = 0 THEN 22 ELSE SortOrder END,
                IsCollapsible = 1,
                DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), N'Fleet Operations'),
                Description = COALESCE(Description, N'Trip lifecycle and route catalog'),
                Visible = 1
            WHERE ModuleKey = @ModuleKey;
            """,
            new { ModuleKey = PlatformModuleKey },
            cancellationToken: cancellationToken));

        var menus = new (string Name, string Route, string Icon, string Perm, int Sort, string Display, string ProductModule)[]
        {
            ("Trips", "/trips", "route", "Trip.View", 10, "Trips", TenantModuleCatalog.TripsModuleCode),
            ("Routes", "/routes", "alt_route", "Route.View", 20, "Routes", TenantModuleCatalog.TravelRoutesModuleCode),
        };

        foreach (var m in menus)
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
                    pm.DisplayName = @Display,
                    pm.Description = NULL,
                    pm.Category = N'Travel',
                    pm.FeatureKey = NULL,
                    pm.ModuleKey = @ProductModule,
                    pm.UpdatedAt = SYSUTCDATETIME()
                FROM PlatformMenus pm
                CROSS JOIN PlatformModules mod
                WHERE mod.ModuleKey = @PlatformModule
                  AND pm.Route = @Route;

                IF @@ROWCOUNT = 0
                INSERT INTO PlatformMenus (
                    ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                    DisplayName, Description, Category, Visible, FeatureKey, ModuleKey,
                    IsMobileSupported, UpdatedAt)
                SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                       @Display, NULL, N'Travel', 1, NULL, @ProductModule, 1, SYSUTCDATETIME()
                FROM PlatformModules mod
                WHERE mod.ModuleKey = @PlatformModule
                  AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                """,
                new
                {
                    m.Name,
                    m.Route,
                    m.Icon,
                    m.Perm,
                    m.Sort,
                    m.Display,
                    m.ProductModule,
                    PlatformModule = PlatformModuleKey
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "TravelFleetOperationsMenuMigration applied (PlatformModules.{Key} → Trips, Routes).",
            PlatformModuleKey);
    }
}
