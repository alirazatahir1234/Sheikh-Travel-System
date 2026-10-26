using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Durable cache for Google Roads Speed Limits lookups (placeId / lat-lng grid cell).
/// Shared across tenants — road limits are geographic, not tenant-specific.
/// </summary>
public static class RoadSpeedLimitCacheMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.RoadSpeedLimitCache', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RoadSpeedLimitCache (
                    CacheKey NVARCHAR(128) NOT NULL CONSTRAINT PK_RoadSpeedLimitCache PRIMARY KEY,
                    PlaceId NVARCHAR(256) NULL,
                    Latitude FLOAT NOT NULL,
                    Longitude FLOAT NOT NULL,
                    SpeedLimitKmh DECIMAL(6,1) NOT NULL,
                    Source NVARCHAR(32) NOT NULL CONSTRAINT DF_RoadSpeedLimitCache_Source DEFAULT N'google_roads',
                    FetchedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_RoadSpeedLimitCache_Fetched DEFAULT SYSUTCDATETIME(),
                    ExpiresAtUtc DATETIME2 NOT NULL
                );

                CREATE INDEX IX_RoadSpeedLimitCache_Expires
                    ON dbo.RoadSpeedLimitCache (ExpiresAtUtc);

                CREATE INDEX IX_RoadSpeedLimitCache_PlaceId
                    ON dbo.RoadSpeedLimitCache (PlaceId)
                    WHERE PlaceId IS NOT NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("RoadSpeedLimitCacheMigration applied (RoadSpeedLimitCache table).");
    }
}
