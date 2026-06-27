using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsDeviceTelemetryMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsDevices', 'LastSpeed') IS NULL
                    ALTER TABLE GpsDevices ADD LastSpeed DECIMAL(8,2) NULL;
                IF COL_LENGTH('GpsDevices', 'LastBatteryLevel') IS NULL
                    ALTER TABLE GpsDevices ADD LastBatteryLevel DECIMAL(5,2) NULL;
                IF COL_LENGTH('GpsDevices', 'LastRssi') IS NULL
                    ALTER TABLE GpsDevices ADD LastRssi INT NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsDeviceTelemetryMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDeviceTelemetryMigration failed.");
        }
    }
}
