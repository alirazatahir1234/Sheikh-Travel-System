using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsTraccarEventMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsAlertEvents', 'ExternalEventId') IS NULL
                BEGIN
                    ALTER TABLE GpsAlertEvents ADD ExternalEventId NVARCHAR(100) NULL;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'UQ_GpsAlertEvents_ExternalEventId'
                      AND object_id = OBJECT_ID('GpsAlertEvents'))
                BEGIN
                    CREATE UNIQUE INDEX UQ_GpsAlertEvents_ExternalEventId
                        ON GpsAlertEvents(ExternalEventId)
                        WHERE ExternalEventId IS NOT NULL AND IsDeleted = 0;
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsTraccarEventMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsTraccarEventMigration failed.");
        }
    }
}
