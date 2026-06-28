using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class TrackerRelayMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TrackerModels')
                   AND COL_LENGTH('TrackerModels', 'DefaultRelayOutput') IS NULL
                    ALTER TABLE TrackerModels ADD DefaultRelayOutput NVARCHAR(50) NULL;

                IF COL_LENGTH('GpsDevices', 'RelayPurpose') IS NULL
                    ALTER TABLE GpsDevices ADD RelayPurpose NVARCHAR(50) NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TrackerModels')
                   AND COL_LENGTH('TrackerModels', 'DefaultRelayOutput') IS NOT NULL
                BEGIN
                    UPDATE TrackerModels
                    SET DefaultRelayOutput = 'output2'
                    WHERE DefaultRelayOutput IS NULL
                      AND (CatalogKey LIKE '%vg03%' OR Name = 'VG03')
                      AND SupportsEngineCutOff = 1;

                    UPDATE TrackerModels
                    SET DefaultRelayOutput = 'output1'
                    WHERE DefaultRelayOutput IS NULL
                      AND SupportsEngineCutOff = 1;
                END

                IF COL_LENGTH('GpsDevices', 'RelayPurpose') IS NOT NULL
                BEGIN
                    UPDATE GpsDevices
                    SET RelayPurpose = 'EngineImmobilizer'
                    WHERE RelayPurpose IS NULL
                      AND SupportsEngineCutoff = 1
                      AND RelayOutput IS NOT NULL;
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("TrackerRelayMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TrackerRelayMigration failed.");
            throw;
        }
    }
}
