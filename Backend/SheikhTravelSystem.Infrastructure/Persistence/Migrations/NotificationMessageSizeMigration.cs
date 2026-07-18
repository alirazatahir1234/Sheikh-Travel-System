using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Widen Notifications.Title/Message so branded email rendering and longer subjects never truncate.
/// </summary>
public static class NotificationMessageSizeMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('Notifications', 'Title') IS NOT NULL
                BEGIN
                    DECLARE @titleType NVARCHAR(128) =
                        (SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                         WHERE TABLE_NAME = 'Notifications' AND COLUMN_NAME = 'Title');
                    DECLARE @titleMax INT =
                        (SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS
                         WHERE TABLE_NAME = 'Notifications' AND COLUMN_NAME = 'Title');
                    IF @titleType = 'nvarchar' AND (@titleMax IS NULL OR (@titleMax > 0 AND @titleMax < 500))
                        ALTER TABLE Notifications ALTER COLUMN Title NVARCHAR(500) NOT NULL;
                END

                IF COL_LENGTH('Notifications', 'Message') IS NOT NULL
                BEGIN
                    DECLARE @msgMax INT =
                        (SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS
                         WHERE TABLE_NAME = 'Notifications' AND COLUMN_NAME = 'Message');
                    -- -1 = MAX; widen anything smaller
                    IF @msgMax IS NULL OR @msgMax > 0
                        ALTER TABLE Notifications ALTER COLUMN Message NVARCHAR(MAX) NOT NULL;
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("NotificationMessageSizeMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationMessageSizeMigration failed.");
            throw;
        }
    }
}
