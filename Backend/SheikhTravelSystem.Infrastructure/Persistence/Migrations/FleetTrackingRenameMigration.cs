using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Renames the "GPS Tracking" sidebar menu item to "Fleet Tracking" in existing databases.
/// </summary>
public static class FleetTrackingRenameMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                -- If both names exist under fleet, retire GPS Tracking (rename target already present).
                UPDATE gps SET gps.IsActive = 0, gps.Name = N'GPS Tracking (legacy)'
                FROM PlatformMenus gps
                INNER JOIN PlatformModules m ON m.Id = gps.ModuleId AND m.ModuleKey = 'fleet'
                WHERE gps.Name = N'GPS Tracking'
                  AND EXISTS (
                      SELECT 1 FROM PlatformMenus ft
                      WHERE ft.ModuleId = gps.ModuleId AND ft.Name = N'Fleet Tracking');

                -- Otherwise rename GPS Tracking → Fleet Tracking when the target name is free.
                UPDATE pm
                SET pm.Name = N'Fleet Tracking'
                FROM PlatformMenus pm
                INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = 'fleet'
                WHERE pm.Name = N'GPS Tracking'
                  AND NOT EXISTS (
                      SELECT 1 FROM PlatformMenus ft
                      WHERE ft.ModuleId = pm.ModuleId AND ft.Name = N'Fleet Tracking');
                """, cancellationToken: cancellationToken));

            logger.LogInformation("FleetTrackingRenameMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FleetTrackingRenameMigration failed.");
            throw;
        }
    }
}
