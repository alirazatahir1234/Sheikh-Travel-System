using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds PlaceName/PlaceType to GpsAddressCache when an older GpsAddressCacheMigration
/// already ran before those columns existed (SchemaMigrationHistory skips re-runs by name).
/// </summary>
public static class GpsAddressCachePlaceColumnsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsAddressCache')
                BEGIN
                    IF COL_LENGTH('GpsAddressCache', 'PlaceName') IS NULL
                        ALTER TABLE GpsAddressCache ADD PlaceName NVARCHAR(200) NULL;
                    IF COL_LENGTH('GpsAddressCache', 'PlaceType') IS NULL
                        ALTER TABLE GpsAddressCache ADD PlaceType NVARCHAR(80) NULL;
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsAddressCachePlaceColumnsMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsAddressCachePlaceColumnsMigration failed.");
            throw;
        }
    }
}
