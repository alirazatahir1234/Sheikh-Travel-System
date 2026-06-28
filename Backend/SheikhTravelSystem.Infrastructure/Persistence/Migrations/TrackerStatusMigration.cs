using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class TrackerStatusMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE GpsDevices
                SET CurrentStatus = 'Available'
                WHERE IsDeleted = 0
                  AND CurrentStatus = 'InStock';

                UPDATE GpsDevices
                SET CurrentStatus = 'Available'
                WHERE IsDeleted = 0
                  AND VehicleId IS NULL
                  AND (CurrentStatus IS NULL OR CurrentStatus = 'Installed');
                """, cancellationToken: cancellationToken));

            logger.LogInformation("TrackerStatusMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TrackerStatusMigration failed.");
            throw;
        }
    }
}
