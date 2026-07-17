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
                    ResolvedAt DATETIME2 NOT NULL,
                    CONSTRAINT UQ_GpsAddressCache_LatLng UNIQUE (LatitudeKey, LongitudeKey)
                );
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsAddressCacheMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsAddressCacheMigration failed.");
        }
    }
}
