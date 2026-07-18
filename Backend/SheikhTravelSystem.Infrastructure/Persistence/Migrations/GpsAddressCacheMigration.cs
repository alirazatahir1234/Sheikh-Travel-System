using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsAddressCacheMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsAddressCache')
                CREATE TABLE GpsAddressCache (
                    Id INT IDENTITY PRIMARY KEY,
                    LatitudeKey DECIMAL(9,4) NOT NULL,
                    LongitudeKey DECIMAL(9,4) NOT NULL,
                    Address NVARCHAR(500) NOT NULL,
                    Road NVARCHAR(200) NULL,
                    City NVARCHAR(120) NULL,
                    State NVARCHAR(120) NULL,
                    Country NVARCHAR(120) NULL,
                    PostalCode NVARCHAR(40) NULL,
                    ResolvedAt DATETIME2 NOT NULL,
                    CONSTRAINT UQ_GpsAddressCache_LatLng UNIQUE (LatitudeKey, LongitudeKey)
                );

                IF COL_LENGTH('GpsAddressCache', 'Road') IS NULL
                    ALTER TABLE GpsAddressCache ADD Road NVARCHAR(200) NULL;
                IF COL_LENGTH('GpsAddressCache', 'City') IS NULL
                    ALTER TABLE GpsAddressCache ADD City NVARCHAR(120) NULL;
                IF COL_LENGTH('GpsAddressCache', 'State') IS NULL
                    ALTER TABLE GpsAddressCache ADD State NVARCHAR(120) NULL;
                IF COL_LENGTH('GpsAddressCache', 'Country') IS NULL
                    ALTER TABLE GpsAddressCache ADD Country NVARCHAR(120) NULL;
                IF COL_LENGTH('GpsAddressCache', 'PostalCode') IS NULL
                    ALTER TABLE GpsAddressCache ADD PostalCode NVARCHAR(40) NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsAddressCacheMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsAddressCacheMigration failed.");
        }
    }
}
