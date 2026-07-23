using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsAlertsLifecycleV2Migration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                IF COL_LENGTH('GpsAlertEvents', 'ReadAt') IS NULL
                    ALTER TABLE GpsAlertEvents ADD ReadAt DATETIME2 NULL;
                IF COL_LENGTH('GpsAlertEvents', 'ReadBy') IS NULL
                    ALTER TABLE GpsAlertEvents ADD ReadBy NVARCHAR(100) NULL;
                IF COL_LENGTH('GpsAlertEvents', 'ArchivedAt') IS NULL
                    ALTER TABLE GpsAlertEvents ADD ArchivedAt DATETIME2 NULL;
                IF COL_LENGTH('GpsAlertEvents', 'ArchivedBy') IS NULL
                    ALTER TABLE GpsAlertEvents ADD ArchivedBy NVARCHAR(100) NULL;
                """,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE GpsAlertEvents
                SET ReadAt = COALESCE(ReadAt, AcknowledgedAt, ResolvedAt),
                    ReadBy = COALESCE(ReadBy, AcknowledgedBy, ResolvedBy)
                WHERE ReadAt IS NULL
                  AND (AcknowledgedAt IS NOT NULL OR ResolvedAt IS NOT NULL);
                """,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAlertEvents_ReadAt')
                    CREATE INDEX IX_GpsAlertEvents_ReadAt ON GpsAlertEvents(ReadAt, Timestamp DESC) WHERE IsDeleted = 0;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAlertEvents_ArchivedAt')
                    CREATE INDEX IX_GpsAlertEvents_ArchivedAt ON GpsAlertEvents(ArchivedAt, Timestamp DESC) WHERE IsDeleted = 0;
                """,
                cancellationToken: cancellationToken));

            logger.LogInformation("GpsAlertsLifecycleV2Migration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsAlertsLifecycleV2Migration failed.");
        }
    }
}
