using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsDeviceUniqueIdMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            if (Exists(
                    "SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_GpsDevices_UniqueId' AND parent_object_id = OBJECT_ID('GpsDevices')",
                    connection))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "ALTER TABLE GpsDevices DROP CONSTRAINT UQ_GpsDevices_UniqueId",
                    cancellationToken: cancellationToken));
            }

            var remediated = await connection.ExecuteAsync(new CommandDefinition("""
                ;WITH Ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY UniqueId
                               ORDER BY CASE WHEN TraccarDeviceId IS NOT NULL THEN 0 ELSE 1 END,
                                        COALESCE(UpdatedAt, CreatedAt) DESC, Id DESC
                           ) AS rn
                    FROM GpsDevices
                    WHERE IsDeleted = 0
                )
                UPDATE gd
                SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                FROM GpsDevices gd
                INNER JOIN Ranked r ON gd.Id = r.Id
                WHERE r.rn > 1
                """, cancellationToken: cancellationToken));

            if (remediated > 0)
                logger.LogWarning("GpsDeviceUniqueIdMigration soft-deleted {Count} duplicate IMEI row(s).", remediated);

            if (!Exists(
                    "SELECT 1 FROM sys.indexes WHERE name = 'UQ_GpsDevices_UniqueId_Active' AND object_id = OBJECT_ID('GpsDevices')",
                    connection))
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    CREATE UNIQUE INDEX UQ_GpsDevices_UniqueId_Active
                        ON GpsDevices(UniqueId)
                        WHERE IsDeleted = 0
                    """, cancellationToken: cancellationToken));
            }

            logger.LogInformation("GpsDeviceUniqueIdMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDeviceUniqueIdMigration failed — duplicate IMEIs may still exist.");
        }
    }

    private static bool Exists(string sql, System.Data.IDbConnection connection) =>
        connection.ExecuteScalar<int>(sql) > 0;
}
