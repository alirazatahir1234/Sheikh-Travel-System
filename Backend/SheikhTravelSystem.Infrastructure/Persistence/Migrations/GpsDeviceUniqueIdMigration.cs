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
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (
                    SELECT 1 FROM sys.key_constraints
                    WHERE name = 'UQ_GpsDevices_UniqueId' AND parent_object_id = OBJECT_ID('GpsDevices'))
                BEGIN
                    ALTER TABLE GpsDevices DROP CONSTRAINT UQ_GpsDevices_UniqueId;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'UQ_GpsDevices_UniqueId_Active'
                      AND object_id = OBJECT_ID('GpsDevices'))
                BEGIN
                    CREATE UNIQUE INDEX UQ_GpsDevices_UniqueId_Active
                        ON GpsDevices(UniqueId)
                        WHERE IsDeleted = 0;
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsDeviceUniqueIdMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDeviceUniqueIdMigration failed.");
        }
    }
}
