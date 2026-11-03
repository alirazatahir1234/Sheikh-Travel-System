using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Repair duplicate GpsDevices.TraccarDeviceId rows that crash position sync
/// (<c>ToDictionary</c> on TraccarDeviceId). Keeps the vehicle-linked / freshest row;
/// clears TraccarDeviceId on losers; soft-deletes name-collision orphans with no history.
/// Does not delete GpsPositions or Vehicles.
/// </summary>
public static class GpsDuplicateTraccarDeviceIdMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            var cleared = await connection.ExecuteAsync(new CommandDefinition("""
                ;WITH Ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY TraccarDeviceId
                               ORDER BY CASE WHEN VehicleId IS NOT NULL THEN 0 ELSE 1 END,
                                        CASE WHEN LastSeenAt IS NOT NULL THEN 0 ELSE 1 END,
                                        LastSeenAt DESC,
                                        Id ASC
                           ) AS rn
                    FROM GpsDevices
                    WHERE IsDeleted = 0 AND TraccarDeviceId IS NOT NULL
                )
                UPDATE gd
                SET TraccarDeviceId = NULL, UpdatedAt = GETUTCDATE()
                FROM GpsDevices gd
                INNER JOIN Ranked r ON gd.Id = r.Id
                WHERE r.rn > 1
                """, cancellationToken: cancellationToken));

            if (cleared > 0)
            {
                logger.LogWarning(
                    "GpsDuplicateTraccarDeviceIdMigration cleared TraccarDeviceId on {Count} duplicate row(s).",
                    cleared);
            }

            // Soft-delete orphan imports that collided by name with a linked tracker and have no history.
            var softDeleted = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE loser
                SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                FROM GpsDevices loser
                WHERE loser.IsDeleted = 0
                  AND loser.VehicleId IS NULL
                  AND loser.TraccarDeviceId IS NULL
                  AND NOT EXISTS (SELECT 1 FROM GpsPositions p WHERE p.GpsDeviceId = loser.Id)
                  AND EXISTS (
                      SELECT 1 FROM GpsDevices keeper
                      WHERE keeper.IsDeleted = 0
                        AND keeper.Id <> loser.Id
                        AND keeper.VehicleId IS NOT NULL
                        AND keeper.Name = loser.Name
                  )
                """, cancellationToken: cancellationToken));

            if (softDeleted > 0)
            {
                logger.LogWarning(
                    "GpsDuplicateTraccarDeviceIdMigration soft-deleted {Count} orphan duplicate name row(s) with no positions.",
                    softDeleted);
            }

            if (!Exists(
                    "SELECT 1 FROM sys.indexes WHERE name = 'UQ_GpsDevices_TraccarDeviceId_Active' AND object_id = OBJECT_ID('GpsDevices')",
                    connection))
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    CREATE UNIQUE INDEX UQ_GpsDevices_TraccarDeviceId_Active
                        ON GpsDevices(TraccarDeviceId)
                        WHERE IsDeleted = 0 AND TraccarDeviceId IS NOT NULL
                    """, cancellationToken: cancellationToken));
            }

            logger.LogInformation("GpsDuplicateTraccarDeviceIdMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDuplicateTraccarDeviceIdMigration failed — duplicate TraccarDeviceId rows may still break position sync.");
            throw;
        }
    }

    private static bool Exists(string sql, System.Data.IDbConnection connection) =>
        connection.ExecuteScalar<int>(sql) > 0;
}
