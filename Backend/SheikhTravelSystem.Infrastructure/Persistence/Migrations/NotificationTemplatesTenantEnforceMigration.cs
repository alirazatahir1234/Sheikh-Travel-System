using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Backfills NotificationTemplates / NotificationRecipients TenantId and enforces NOT NULL on templates.
/// </summary>
public static class NotificationTemplatesTenantEnforceMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('NotificationTemplates', 'TenantId') IS NULL
                    ALTER TABLE NotificationTemplates ADD TenantId INT NULL;

                IF COL_LENGTH('NotificationRecipients', 'TenantId') IS NULL
                    ALTER TABLE NotificationRecipients ADD TenantId INT NULL;

                UPDATE t
                SET t.TenantId = COALESCE(t.TenantId, 1)
                FROM NotificationTemplates t
                WHERE t.TenantId IS NULL;

                UPDATE r
                SET r.TenantId = COALESCE(r.TenantId, n.TenantId, u.TenantId, 1)
                FROM NotificationRecipients r
                LEFT JOIN Notifications n ON n.Id = r.NotificationId
                LEFT JOIN Users u ON u.Id = r.UserId
                WHERE r.TenantId IS NULL;

                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID('NotificationTemplates')
                      AND name = 'TenantId'
                      AND is_nullable = 1)
                BEGIN
                    ALTER TABLE NotificationTemplates ALTER COLUMN TenantId INT NOT NULL;
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("NotificationTemplatesTenantEnforceMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationTemplatesTenantEnforceMigration failed.");
            throw;
        }
    }
}
