using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Introduces the major <c>ALERTS</c> module and per-type alert children.
/// Moves <c>ROAD_SPEED_ALERTS</c> under Alerts (depends on ALERTS, category Alerts).
/// Backfills ALERTS + default alert types for tenants that already have GPS.
/// </summary>
public static class ModuleAlertsRestructureMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (!await TableExistsAsync(connection, "Modules", cancellationToken))
            return;

        await UpsertModuleAsync(connection, new ModuleRow(
            AlertModuleCatalog.AlertsModuleCode,
            "Fleet Alerts",
            "Fleet Alerts",
            "Fleet alert types — enable the parent and toggle individual alert capabilities per company",
            AlertModuleCatalog.AlertsCategory,
            "notifications_active",
            "/gps-tracking/alerts",
            200,
            """["GPS"]""",
            """["alerts"]"""), cancellationToken);

        foreach (var t in AlertModuleCatalog.Types)
        {
            var deps = $"""["{AlertModuleCatalog.AlertsModuleCode}"]""";
            var legacy = JsonSerializer.Serialize(new[] { t.LegacyKey });
            await UpsertModuleAsync(connection, new ModuleRow(
                t.ModuleCode,
                t.Name,
                t.DisplayName,
                t.Description,
                AlertModuleCatalog.AlertsCategory,
                t.Icon,
                "/gps-tracking/alerts",
                t.SortOrder,
                deps,
                legacy), cancellationToken);
        }

        if (!await TableExistsAsync(connection, "TenantModules", cancellationToken))
        {
            logger.LogInformation("ModuleAlertsRestructureMigration applied (Modules only; no TenantModules).");
            return;
        }

        // Tenants with GPS (TenantModules or legacy gps-tracking) get ALERTS + default alert types.
        // Also cover tenants that already had ROAD_SPEED_ALERTS.
        var gpsTenantIds = (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT DISTINCT TenantId FROM (
                SELECT tm.TenantId
                FROM TenantModules tm
                INNER JOIN Modules g ON g.Id = tm.ModuleId
                WHERE g.ModuleCode IN (N'GPS', N'ROAD_SPEED_ALERTS')
                UNION
                SELECT t.Id
                FROM Tenants t
                WHERE t.EnabledModulesJson IS NOT NULL
                  AND (
                    t.EnabledModulesJson LIKE N'%"gps-tracking"%'
                    OR t.EnabledModulesJson LIKE N'%"GPS"%'
                    OR t.EnabledModulesJson LIKE N'%"road-speed-alerts"%'
                  )
            ) x
            """, cancellationToken: cancellationToken))).ToList();

        var defaultCodes = AlertModuleCatalog.DefaultModuleCodes;
        var backfilled = 0;
        foreach (var tenantId in gpsTenantIds)
        {
            foreach (var code in defaultCodes)
            {
                backfilled += await connection.ExecuteAsync(new CommandDefinition("""
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

        // Anyone with ROAD_SPEED must also have ALERTS parent.
        var alertsParent = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantModules (TenantId, ModuleId)
            SELECT tm.TenantId, alerts.Id
            FROM TenantModules tm
            INNER JOIN Modules road ON road.Id = tm.ModuleId AND road.ModuleCode = N'ROAD_SPEED_ALERTS'
            CROSS JOIN Modules alerts
            WHERE alerts.ModuleCode = N'ALERTS'
              AND NOT EXISTS (
                  SELECT 1 FROM TenantModules x
                  WHERE x.TenantId = tm.TenantId AND x.ModuleId = alerts.Id)
            """, cancellationToken: cancellationToken));

        // Re-sync EnabledModulesJson for tenants that received new modules.
        if (await ColumnExistsAsync(connection, "Tenants", "EnabledModulesJson", cancellationToken))
        {
            var tenantIds = (await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT DISTINCT tm.TenantId
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE m.ModuleCode = N'ALERTS'
                """, cancellationToken: cancellationToken))).ToList();

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
            "ModuleAlertsRestructureMigration applied. DefaultAlertBackfillRows={Backfill}, AlertsParentForRoadSpeed={Parent}",
            backfilled, alertsParent);
    }

    private sealed record ModuleRow(
        string Code,
        string Name,
        string DisplayName,
        string Description,
        string Category,
        string Icon,
        string Route,
        int SortOrder,
        string DependenciesJson,
        string LegacyKeysJson);

    private static async Task UpsertModuleAsync(
        IDbConnection connection,
        ModuleRow row,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = @Code)
                UPDATE Modules SET
                    ModuleName = @Name,
                    DisplayName = @DisplayName,
                    Description = @Description,
                    Category = @Category,
                    Icon = @Icon,
                    Route = @Route,
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
                    N'1.0.0', @Icon, @Route, @SortOrder, @DependenciesJson, 1,
                    1, 0, 1, N'Active', @LegacyKeysJson);
            """,
            new
            {
                row.Code,
                row.Name,
                row.DisplayName,
                row.Description,
                row.Category,
                row.Icon,
                row.Route,
                row.SortOrder,
                row.DependenciesJson,
                row.LegacyKeysJson
            },
            cancellationToken: cancellationToken));
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
