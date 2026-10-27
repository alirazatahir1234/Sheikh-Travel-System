using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Registers ALERT_FUEL_STATION_STOP module and enables it for tenants that already have ALERTS.
/// </summary>
public static class FuelStationAlertModuleMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (!await TableExistsAsync(connection, "Modules", cancellationToken))
        {
            logger.LogInformation("FuelStationAlertModuleMigration skipped (Modules table missing).");
            return;
        }

        const string code = "ALERT_FUEL_STATION_STOP";
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = @Code)
                INSERT INTO Modules (ModuleCode, ModuleName, DisplayName, Description, Category, Icon, Route, SortOrder,
                    DependenciesJson, Visible, IsMobileSupported, IsAISupported, IsGPSSupported, Status, LegacyKeysJson)
                VALUES (@Code, N'Fuel Station Stop', N'Fuel Station Stop',
                    N'GPS stop detection near petrol-station POIs (Google Places)',
                    N'Fleet', N'local_gas_station', N'/gps-tracking/alerts', 214,
                    N'["ALERTS","GPS"]', 1, 1, 0, 1, N'Active', N'["alert-fuel-station-stop"]');
            ELSE
                UPDATE Modules SET
                    ModuleName = N'Fuel Station Stop',
                    DisplayName = N'Fuel Station Stop',
                    Description = N'GPS stop detection near petrol-station POIs (Google Places)',
                    SortOrder = 214,
                    LegacyKeysJson = N'["alert-fuel-station-stop"]',
                    Status = N'Active'
                WHERE ModuleCode = @Code;
            """, new { Code = code }, cancellationToken: cancellationToken));

        if (!await TableExistsAsync(connection, "TenantModules", cancellationToken))
        {
            logger.LogInformation("FuelStationAlertModuleMigration applied (Modules only).");
            return;
        }

        var enabled = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantModules (TenantId, ModuleId)
            SELECT DISTINCT tm.TenantId, m.Id
            FROM TenantModules tm
            INNER JOIN Modules parent ON parent.Id = tm.ModuleId AND parent.ModuleCode = N'ALERTS'
            CROSS JOIN Modules m
            WHERE m.ModuleCode = @Code
              AND NOT EXISTS (
                  SELECT 1 FROM TenantModules x
                  WHERE x.TenantId = tm.TenantId AND x.ModuleId = m.Id)
            """, new { Code = code }, cancellationToken: cancellationToken));

        logger.LogInformation("FuelStationAlertModuleMigration applied. TenantBackfillRows={Rows}", enabled);
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.IDbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(@Table, 'U') IS NOT NULL THEN 1 ELSE 0 END",
            new { Table = table },
            cancellationToken: cancellationToken)) == 1;
    }
}
