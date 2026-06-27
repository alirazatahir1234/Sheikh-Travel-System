using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsTraccarMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsDevices', 'TraccarDeviceId') IS NULL
                    ALTER TABLE GpsDevices ADD TraccarDeviceId INT NULL;

                IF COL_LENGTH('GpsDevices', 'Model') IS NULL
                    ALTER TABLE GpsDevices ADD Model NVARCHAR(100) NULL;

                IF COL_LENGTH('GpsDevices', 'SimNumber') IS NULL
                    ALTER TABLE GpsDevices ADD SimNumber NVARCHAR(50) NULL;

                IF COL_LENGTH('GpsDevices', 'Vendor') IS NULL
                    ALTER TABLE GpsDevices ADD Vendor NVARCHAR(100) NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_GpsDevices_TraccarDeviceId'
                      AND object_id = OBJECT_ID('GpsDevices'))
                    CREATE INDEX IX_GpsDevices_TraccarDeviceId
                        ON GpsDevices(TraccarDeviceId)
                        WHERE TraccarDeviceId IS NOT NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsTraccarMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsTraccarMigration failed.");
        }
    }
}
