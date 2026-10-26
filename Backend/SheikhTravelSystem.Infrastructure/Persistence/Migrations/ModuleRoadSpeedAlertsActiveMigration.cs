using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Text.Json;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Registers ROAD_SPEED_ALERTS as an Active enableable module (legacy key road-speed-alerts).
/// Opt-in only — does not auto-enable for existing GPS tenants.
/// </summary>
public static class ModuleRoadSpeedAlertsActiveMigration
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
                    Description = N'Alert when vehicle speed exceeds the posted road speed limit by a configurable margin (Google Roads Speed Limits)',
                    Category = N'Fleet',
                    Icon = N'speed',
                    Route = N'/gps-tracking/alerts',
                    SortOrder = 31,
                    DependenciesJson = N'["GPS"]',
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
                    N'Alert when vehicle speed exceeds the posted road speed limit by a configurable margin (Google Roads Speed Limits)', N'Fleet',
                    N'1.0.0', N'speed', N'/gps-tracking/alerts', 31, N'["GPS"]', 1,
                    1, 0, 1, N'Active', N'["road-speed-alerts"]');
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "ModuleRoadSpeedAlertsActiveMigration applied (ROAD_SPEED_ALERTS → Active enableable). Keys={Keys}",
            JsonSerializer.Serialize(new[] { "road-speed-alerts" }));
    }
}
