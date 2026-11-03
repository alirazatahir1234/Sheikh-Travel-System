using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// GPS becomes a core TenantModule (always-on). Geofencing, speed-alerts, and route-playback
/// move to TenantFeatures add-ons. Backfills existing tenants without removing capabilities.
/// </summary>
public static class GpsCoreAndAddOnFeaturesMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (!await TableExistsAsync(connection, "FeatureDefinitions", cancellationToken))
        {
            logger.LogWarning("GpsCoreAndAddOnFeaturesMigration skipped — FeatureDefinitions missing.");
            return;
        }

        // 1) Upsert GPS add-on FeatureDefinitions.
        foreach (var addOn in GpsAddOnFeatureCatalog.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM FeatureDefinitions WHERE FeatureKey = @FeatureKey)
                    INSERT INTO FeatureDefinitions (
                        FeatureKey, ModuleKey, Name, DisplayName, Description, Category, Icon, Route,
                        SortOrder, Visible, Status, IsMobileSupported, IsAISupported, IsGPSSupported, IsActive)
                    VALUES (
                        @FeatureKey, N'GPS', @Name, @Name, @Description, N'GPS Add-ons', @Icon, @Route,
                        @SortOrder, 1, N'Active', 1, 0, 1, 1);
                ELSE
                    UPDATE FeatureDefinitions
                    SET ModuleKey = N'GPS',
                        Name = @Name,
                        DisplayName = @Name,
                        Description = @Description,
                        Category = N'GPS Add-ons',
                        Icon = @Icon,
                        Route = @Route,
                        SortOrder = @SortOrder,
                        Visible = 1,
                        Status = N'Active',
                        IsMobileSupported = 1,
                        IsGPSSupported = 1,
                        IsActive = 1
                    WHERE FeatureKey = @FeatureKey;
                """,
                new
                {
                    addOn.FeatureKey,
                    Name = addOn.DisplayName,
                    addOn.Description,
                    addOn.Icon,
                    Route = addOn.FeatureKey == GpsAddOnFeatureCatalog.RoutePlayback
                        ? "/gps-tracking/history"
                        : addOn.FeatureKey == GpsAddOnFeatureCatalog.Geofencing
                            ? "/gps-tracking/geofences"
                            : "/gps-tracking/alerts",
                    addOn.SortOrder
                },
                cancellationToken: cancellationToken));
        }

        // Mark module-legacy FeatureDefinitions so they are not treated as toggles.
        foreach (var legacy in FeatureRegistrySeed.All
                     .Where(e => string.Equals(e.Status, FeatureRegistrySeed.ModuleLegacyStatus, StringComparison.OrdinalIgnoreCase)))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM FeatureDefinitions WHERE FeatureKey = @FeatureKey)
                    UPDATE FeatureDefinitions
                    SET Status = N'ModuleLegacy', Category = COALESCE(Category, N'Administration')
                    WHERE FeatureKey = @FeatureKey
                      AND (Status IS NULL OR Status <> N'ModuleLegacy');
                """, new { legacy.FeatureKey }, cancellationToken: cancellationToken));
        }

        if (!await TableExistsAsync(connection, "Modules", cancellationToken)
            || !await TableExistsAsync(connection, "TenantModules", cancellationToken))
        {
            logger.LogInformation("GpsCoreAndAddOnFeaturesMigration applied FeatureDefinitions only (no TenantModules).");
            return;
        }

        // 2) Ensure GPS module row exists.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = N'GPS')
                INSERT INTO Modules (ModuleCode, ModuleName, LegacyKeysJson, Status)
                VALUES (N'GPS', N'Fleet Tracking', N'["gps-tracking"]', N'Active');
            ELSE
                UPDATE Modules SET Status = N'Active' WHERE ModuleCode = N'GPS';
            """, cancellationToken: cancellationToken));

        // 3) Backfill GPS TenantModules for tenants that already had GPS or GPS-related entitlements.
        var gpsBackfill = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantModules (TenantId, ModuleId)
            SELECT DISTINCT candidates.TenantId, g.Id
            FROM (
                SELECT tm.TenantId
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE m.ModuleCode IN (
                    N'GPS', N'ALERTS', N'ALERT_OVERSPEED', N'ROAD_SPEED_ALERTS',
                    N'ALERT_GEOFENCE_ENTER', N'ALERT_GEOFENCE_EXIT', N'ALERT_GPS_LOST')
                UNION
                SELECT t.Id
                FROM Tenants t
                WHERE t.IsActive = 1
                  AND t.EnabledModulesJson IS NOT NULL
                  AND (
                        t.EnabledModulesJson LIKE N'%"gps-tracking"%'
                     OR t.EnabledModulesJson LIKE N'%"gps"%'
                     OR t.EnabledModulesJson LIKE N'%gps-tracking%'
                  )
            ) candidates
            CROSS JOIN (SELECT Id FROM Modules WHERE ModuleCode = N'GPS') g
            WHERE NOT EXISTS (
                SELECT 1 FROM TenantModules x
                WHERE x.TenantId = candidates.TenantId AND x.ModuleId = g.Id);
            """, cancellationToken: cancellationToken));

        if (!await TableExistsAsync(connection, "TenantFeatures", cancellationToken))
        {
            logger.LogInformation(
                "GpsCoreAndAddOnFeaturesMigration: GPS backfill={GpsBackfill}; TenantFeatures missing.",
                gpsBackfill);
            return;
        }

        // 4) Geofencing — tenants that had geofence alert modules OR already had GPS (preserve capability).
        var geo = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantFeatures (TenantId, FeatureKey, IsEnabled, EnabledDate, LastModified)
            SELECT DISTINCT src.TenantId, N'geofencing', 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM (
                SELECT tm.TenantId
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE m.ModuleCode IN (N'ALERT_GEOFENCE_ENTER', N'ALERT_GEOFENCE_EXIT', N'GPS')
            ) src
            WHERE EXISTS (SELECT 1 FROM FeatureDefinitions fd WHERE fd.FeatureKey = N'geofencing')
              AND NOT EXISTS (
                  SELECT 1 FROM TenantFeatures tf
                  WHERE tf.TenantId = src.TenantId AND tf.FeatureKey = N'geofencing');
            """, cancellationToken: cancellationToken));

        // 5) Speed alerts — only tenants that had overspeed / road-speed modules.
        var speed = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantFeatures (TenantId, FeatureKey, IsEnabled, EnabledDate, LastModified)
            SELECT DISTINCT src.TenantId, N'speed-alerts', 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM (
                SELECT tm.TenantId
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE m.ModuleCode IN (N'ALERT_OVERSPEED', N'ROAD_SPEED_ALERTS')
            ) src
            WHERE EXISTS (SELECT 1 FROM FeatureDefinitions fd WHERE fd.FeatureKey = N'speed-alerts')
              AND NOT EXISTS (
                  SELECT 1 FROM TenantFeatures tf
                  WHERE tf.TenantId = src.TenantId AND tf.FeatureKey = N'speed-alerts');
            """, cancellationToken: cancellationToken));

        // 6) Route playback — tenants with GPS (was part of core tracking UX).
        var playback = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantFeatures (TenantId, FeatureKey, IsEnabled, EnabledDate, LastModified)
            SELECT DISTINCT tm.TenantId, N'route-playback', 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId AND m.ModuleCode = N'GPS'
            WHERE EXISTS (SELECT 1 FROM FeatureDefinitions fd WHERE fd.FeatureKey = N'route-playback')
              AND NOT EXISTS (
                  SELECT 1 FROM TenantFeatures tf
                  WHERE tf.TenantId = tm.TenantId AND tf.FeatureKey = N'route-playback');
            """, cancellationToken: cancellationToken));

        // 7) Mark converted alert module rows as catalog-only (Status) so they drop out of enableable UI.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Modules
            SET Status = N'Active',
                Description = CASE
                    WHEN ModuleCode IN (N'ALERT_OVERSPEED', N'ROAD_SPEED_ALERTS')
                        THEN N'GPS add-on: speed-alerts (TenantFeatures) — not a separate module toggle'
                    WHEN ModuleCode IN (N'ALERT_GEOFENCE_ENTER', N'ALERT_GEOFENCE_EXIT')
                        THEN N'GPS add-on: geofencing (TenantFeatures) — not a separate module toggle'
                    ELSE Description
                END
            WHERE ModuleCode IN (
                N'ALERT_OVERSPEED', N'ROAD_SPEED_ALERTS',
                N'ALERT_GEOFENCE_ENTER', N'ALERT_GEOFENCE_EXIT');
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "GpsCoreAndAddOnFeaturesMigration applied (GPS backfill={Gps}, geofencing={Geo}, speed-alerts={Speed}, route-playback={Playback}).",
            gpsBackfill, geo, speed, playback);
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.IDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(@Name, N'U') IS NULL THEN 0 ELSE 1 END
            """, new { Name = tableName }, cancellationToken: cancellationToken));
        return n == 1;
    }
}
