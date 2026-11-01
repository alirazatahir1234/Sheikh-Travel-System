using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds assignee fields on GpsAlertEvents so ack ≠ assign ≠ resolve stay independent.
/// </summary>
public static class GpsAlertAssignmentMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                IF COL_LENGTH('GpsAlertEvents', 'AssignedToUserId') IS NULL
                    ALTER TABLE GpsAlertEvents ADD AssignedToUserId INT NULL;
                IF COL_LENGTH('GpsAlertEvents', 'AssignedToName') IS NULL
                    ALTER TABLE GpsAlertEvents ADD AssignedToName NVARCHAR(100) NULL;
                IF COL_LENGTH('GpsAlertEvents', 'AssignedAt') IS NULL
                    ALTER TABLE GpsAlertEvents ADD AssignedAt DATETIME2 NULL;
                IF COL_LENGTH('GpsAlertEvents', 'AssignedBy') IS NULL
                    ALTER TABLE GpsAlertEvents ADD AssignedBy NVARCHAR(100) NULL;
                """,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAlertEvents_AssignedToUserId')
                    CREATE INDEX IX_GpsAlertEvents_AssignedToUserId
                    ON GpsAlertEvents(AssignedToUserId, Timestamp DESC)
                    WHERE IsDeleted = 0 AND AssignedToUserId IS NOT NULL;
                """,
                cancellationToken: cancellationToken));

            logger.LogInformation("GpsAlertAssignmentMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsAlertAssignmentMigration failed.");
        }
    }
}
