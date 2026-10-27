using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Marks ROAD_SPEED_ALERTS as a premium paid subscription add-on (parity with AI):
/// Active enableable child of ALERTS, opt-in, not auto-enabled.
/// Description / deps aligned with SubscriptionAddOnCatalog + AlertModuleCatalog.
/// </summary>
public static class ModuleRoadSpeedAlertsPremiumAddOnMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID('Modules', 'U') IS NULL RETURN;

            IF EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = N'ROAD_SPEED_ALERTS')
                UPDATE Modules SET
                    ModuleName = N'Road Speed Alerts',
                    DisplayName = N'Road Speed Alerts',
                    Description = N'Premium overspeed alerts using posted road speed limits (Google Roads Speed Limits)',
                    Category = N'Alerts',
                    Icon = N'speed',
                    Route = N'/gps-tracking/alerts',
                    SortOrder = 204,
                    DependenciesJson = N'["ALERTS"]',
                    Visible = 1,
                    IsMobileSupported = 1,
                    IsAISupported = 0,
                    IsGPSSupported = 1,
                    Status = N'Active',
                    LegacyKeysJson = N'["road-speed-alerts"]'
                WHERE ModuleCode = N'ROAD_SPEED_ALERTS';
            ELSE
                INSERT INTO Modules (
                    ModuleCode, ModuleName, DisplayName, Description, Category,
                    Version, Icon, Route, SortOrder, DependenciesJson, Visible,
                    IsMobileSupported, IsAISupported, IsGPSSupported, Status, LegacyKeysJson)
                VALUES (
                    N'ROAD_SPEED_ALERTS', N'Road Speed Alerts', N'Road Speed Alerts',
                    N'Premium overspeed alerts using posted road speed limits (Google Roads Speed Limits)', N'Alerts',
                    N'1.0.0', N'speed', N'/gps-tracking/alerts', 204, N'["ALERTS"]', 1,
                    1, 0, 1, N'Active', N'["road-speed-alerts"]');
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "ModuleRoadSpeedAlertsPremiumAddOnMigration applied (ROAD_SPEED_ALERTS → premium subscription add-on under ALERTS).");
    }
}
