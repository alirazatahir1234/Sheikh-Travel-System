using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Aligns alert-type premium tagging with product rules:
/// <list type="bullet">
/// <item>Basic alerts stay enabled for GPS/ALERTS tenants (no premium label; do not disable).</item>
/// <item>Premium alerts (Fixed Speed, Road Speed, GPS Lost, Low Fuel) are opt-in — remove from
/// TenantModules when previously auto-enabled; never in plan defaults.</item>
/// </list>
/// Also refreshes Modules metadata and SubscriptionPlans.DefaultModuleCodesJson from catalogs.
/// </summary>
public static class ModuleAlertPremiumTagsMigration
{
    private static readonly string[] PremiumAlertCodes =
    [
        "ALERT_OVERSPEED",
        "ROAD_SPEED_ALERTS",
        "ALERT_GPS_LOST",
        "ALERT_LOW_FUEL",
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (!await TableExistsAsync(connection, "Modules", cancellationToken))
            return;

        // Refresh Modules rows from AlertModuleCatalog (descriptions / sort / deps).
        foreach (var t in AlertModuleCatalog.Types)
        {
            var deps = $"""["{AlertModuleCatalog.AlertsModuleCode}"]""";
            var legacy = JsonSerializer.Serialize(new[] { t.LegacyKey });
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = @Code)
                    UPDATE Modules SET
                        ModuleName = @Name,
                        DisplayName = @DisplayName,
                        Description = @Description,
                        Category = @Category,
                        Icon = @Icon,
                        Route = N'/gps-tracking/alerts',
                        SortOrder = @SortOrder,
                        DependenciesJson = @DependenciesJson,
                        Visible = 1,
                        IsMobileSupported = 1,
                        IsAISupported = 0,
                        IsGPSSupported = 1,
                        Status = N'Active',
                        LegacyKeysJson = @LegacyKeysJson
                    WHERE ModuleCode = @Code;
                ELSE
                    INSERT INTO Modules (
                        ModuleCode, ModuleName, DisplayName, Description, Category,
                        Version, Icon, Route, SortOrder, DependenciesJson, Visible,
                        IsMobileSupported, IsAISupported, IsGPSSupported, Status, LegacyKeysJson)
                    VALUES (
                        @Code, @Name, @DisplayName, @Description, @Category,
                        N'1.0.0', @Icon, N'/gps-tracking/alerts', @SortOrder, @DependenciesJson, 1,
                        1, 0, 1, N'Active', @LegacyKeysJson);
                """,
                new
                {
                    Code = t.ModuleCode,
                    t.Name,
                    t.DisplayName,
                    t.Description,
                    Category = AlertModuleCatalog.AlertsCategory,
                    t.Icon,
                    t.SortOrder,
                    DependenciesJson = deps,
                    LegacyKeysJson = legacy
                },
                cancellationToken: cancellationToken));
        }

        // Refresh plan default module packs from SubscriptionPlanCatalog (excludes premium alerts).
        if (await TableExistsAsync(connection, "SubscriptionPlans", cancellationToken)
            && await ColumnExistsAsync(connection, "SubscriptionPlans", "DefaultModuleCodesJson", cancellationToken))
        {
            foreach (var plan in SubscriptionPlanCatalog.All)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE SubscriptionPlans
                    SET DefaultModuleCodesJson = @Json
                    WHERE SubscriptionCode = @Code
                    """,
                    new
                    {
                        Code = plan.SubscriptionCode,
                        Json = SubscriptionPlanCatalog.SerializeModuleCodes(plan.DefaultModuleCodes)
                    },
                    cancellationToken: cancellationToken));
            }
        }

        if (!await TableExistsAsync(connection, "TenantModules", cancellationToken))
        {
            logger.LogInformation(
                "ModuleAlertPremiumTagsMigration applied (Modules/plans only; no TenantModules).");
            return;
        }

        // Tenants that already have GPS or ALERTS (or legacy gps-tracking).
        var alertTenantIds = (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT DISTINCT TenantId FROM (
                SELECT tm.TenantId
                FROM TenantModules tm
                INNER JOIN Modules g ON g.Id = tm.ModuleId
                WHERE g.ModuleCode IN (N'GPS', N'ALERTS')
                UNION
                SELECT t.Id
                FROM Tenants t
                WHERE t.EnabledModulesJson IS NOT NULL
                  AND (
                    t.EnabledModulesJson LIKE N'%"gps-tracking"%'
                    OR t.EnabledModulesJson LIKE N'%"GPS"%'
                    OR t.EnabledModulesJson LIKE N'%"alerts"%'
                    OR t.EnabledModulesJson LIKE N'%"ALERTS"%'
                  )
            ) x
            """, cancellationToken: cancellationToken))).ToList();

        // Basic alerts ON — never disable; backfill missing default-on codes only.
        var basicCodes = AlertModuleCatalog.DefaultModuleCodes;
        var basicBackfill = 0;
        foreach (var tenantId in alertTenantIds)
        {
            foreach (var code in basicCodes)
            {
                basicBackfill += await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO TenantModules (TenantId, ModuleId)
                    SELECT @TenantId, m.Id
                    FROM Modules m
                    WHERE m.ModuleCode = @Code
                      AND NOT EXISTS (
                          SELECT 1 FROM TenantModules x
                          WHERE x.TenantId = @TenantId AND x.ModuleId = m.Id)
                    """, new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));
            }
        }

        // Premium alerts OFF by default — remove auto-enabled TenantModules rows.
        // ROAD_SPEED_ALERTS was never in defaults; still clear OVERSPEED / GPS_LOST / LOW_FUEL
        // that ModuleAlertsRestructureMigration may have backfilled. Explicit opt-in after this
        // migration must re-enable via Module Management.
        var premiumRemoved = 0;
        foreach (var code in PremiumAlertCodes)
        {
            // Keep ROAD_SPEED_ALERTS if already enabled (true opt-in before this change).
            // Only strip the three that were incorrectly treated as default-on.
            if (string.Equals(code, "ROAD_SPEED_ALERTS", StringComparison.OrdinalIgnoreCase))
                continue;

            premiumRemoved += await connection.ExecuteAsync(new CommandDefinition("""
                DELETE tm
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE m.ModuleCode = @Code
                """, new { Code = code }, cancellationToken: cancellationToken));
        }

        // Re-sync EnabledModulesJson for affected tenants.
        if (await ColumnExistsAsync(connection, "Tenants", "EnabledModulesJson", cancellationToken))
        {
            var tenantIds = alertTenantIds;
            if (tenantIds.Count == 0)
            {
                tenantIds = (await connection.QueryAsync<int>(new CommandDefinition("""
                    SELECT DISTINCT tm.TenantId
                    FROM TenantModules tm
                    INNER JOIN Modules m ON m.Id = tm.ModuleId
                    WHERE m.ModuleCode = N'ALERTS'
                    """, cancellationToken: cancellationToken))).ToList();
            }

            foreach (var tenantId in tenantIds)
            {
                var codes = (await connection.QueryAsync<string>(new CommandDefinition("""
                    SELECT m.ModuleCode
                    FROM TenantModules tm
                    INNER JOIN Modules m ON m.Id = tm.ModuleId
                    WHERE tm.TenantId = @TenantId
                    """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

                var json = TenantModuleCatalog.SerializeLegacyKeys(
                    TenantModuleCatalog.LegacyKeysFromCodes(codes));

                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE Tenants SET EnabledModulesJson = @Json, UpdatedAt = GETUTCDATE()
                    WHERE Id = @TenantId
                    """, new { TenantId = tenantId, Json = json }, cancellationToken: cancellationToken));
            }
        }

        logger.LogInformation(
            "ModuleAlertPremiumTagsMigration applied. BasicBackfill={Basic}, PremiumRemoved={Premium}. Basic alerts remain enabled; premium default-off.",
            basicBackfill, premiumRemoved);
    }

    private static async Task<bool> TableExistsAsync(
        IDbConnection connection, string table, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT OBJECT_ID(@Name, 'U')",
            new { Name = table },
            cancellationToken: cancellationToken));
        return id is > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        IDbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM sys.columns
            WHERE object_id = OBJECT_ID(@Table, 'U') AND name = @Column
            """, new { Table = table, Column = column }, cancellationToken: cancellationToken));
        return n > 0;
    }
}
