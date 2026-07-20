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
                UPDATE pm
                SET pm.Name = N'Fleet Tracking'
                FROM PlatformMenus pm
                INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = 'fleet'
                WHERE pm.Name = N'GPS Tracking';
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
