using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds tenant boundaries to notification center tables.
/// </summary>
public static class NotificationTenantIsolationMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('Notifications', 'TenantId') IS NULL
                    ALTER TABLE Notifications ADD TenantId INT NULL;

                IF COL_LENGTH('NotificationRecipients', 'TenantId') IS NULL
                    ALTER TABLE NotificationRecipients ADD TenantId INT NULL;

                IF COL_LENGTH('NotificationDeliveryLogs', 'TenantId') IS NULL
                    ALTER TABLE NotificationDeliveryLogs ADD TenantId INT NULL;

                IF COL_LENGTH('NotificationTemplates', 'TenantId') IS NULL
                    ALTER TABLE NotificationTemplates ADD TenantId INT NULL;

                IF COL_LENGTH('NotificationPreferences', 'TenantId') IS NULL
                    ALTER TABLE NotificationPreferences ADD TenantId INT NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE n
                SET n.TenantId = COALESCE(n.TenantId, u.TenantId, 1)
                FROM Notifications n
                LEFT JOIN Users u ON u.Id = n.UserId
                WHERE n.TenantId IS NULL;

                UPDATE r
                SET r.TenantId = COALESCE(r.TenantId, u.TenantId, n.TenantId, 1)
                FROM NotificationRecipients r
                LEFT JOIN Users u ON u.Id = r.UserId
                LEFT JOIN Notifications n ON n.Id = r.NotificationId
                WHERE r.TenantId IS NULL;

                UPDATE l
                SET l.TenantId = COALESCE(l.TenantId, n.TenantId, 1)
                FROM NotificationDeliveryLogs l
                LEFT JOIN Notifications n ON n.Id = l.NotificationId
                WHERE l.TenantId IS NULL;

                UPDATE t
                SET t.TenantId = COALESCE(t.TenantId, 1)
                FROM NotificationTemplates t
                WHERE t.TenantId IS NULL;

                UPDATE p
                SET p.TenantId = COALESCE(p.TenantId, u.TenantId, 1)
                FROM NotificationPreferences p
                LEFT JOIN Users u ON u.Id = p.UserId
                WHERE p.TenantId IS NULL;
                """, cancellationToken: cancellationToken));

            // INCLUDE only columns that exist on this DB (schema drift across environments).
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_TenantId_UserId_CreatedAt' AND object_id = OBJECT_ID('Notifications'))
                BEGIN
                    DECLARE @notifInclude NVARCHAR(500) = N'';
                    IF COL_LENGTH('Notifications', 'IsDeleted') IS NOT NULL SET @notifInclude = @notifInclude + N'IsDeleted,';
                    IF COL_LENGTH('Notifications', 'IsArchived') IS NOT NULL SET @notifInclude = @notifInclude + N'IsArchived,';
                    IF COL_LENGTH('Notifications', 'IsRead') IS NOT NULL SET @notifInclude = @notifInclude + N'IsRead,';
                    IF COL_LENGTH('Notifications', 'Channel') IS NOT NULL SET @notifInclude = @notifInclude + N'Channel,';
                    IF COL_LENGTH('Notifications', 'Priority') IS NOT NULL SET @notifInclude = @notifInclude + N'Priority,';
                    IF COL_LENGTH('Notifications', 'Module') IS NOT NULL SET @notifInclude = @notifInclude + N'Module,';
                    IF LEN(@notifInclude) > 0
                    BEGIN
                        SET @notifInclude = LEFT(@notifInclude, LEN(@notifInclude) - 1);
                        EXEC(N'CREATE INDEX IX_Notifications_TenantId_UserId_CreatedAt ON Notifications (TenantId, UserId, CreatedAt DESC) INCLUDE (' + @notifInclude + N')');
                    END
                    ELSE
                        CREATE INDEX IX_Notifications_TenantId_UserId_CreatedAt ON Notifications (TenantId, UserId, CreatedAt DESC);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationRecipients_TenantId_UserId_NotificationId' AND object_id = OBJECT_ID('NotificationRecipients'))
                BEGIN
                    DECLARE @recipInclude NVARCHAR(500) = N'';
                    IF COL_LENGTH('NotificationRecipients', 'IsRead') IS NOT NULL SET @recipInclude = @recipInclude + N'IsRead,';
                    IF COL_LENGTH('NotificationRecipients', 'IsDeleted') IS NOT NULL SET @recipInclude = @recipInclude + N'IsDeleted,';
                    IF COL_LENGTH('NotificationRecipients', 'IsArchived') IS NOT NULL SET @recipInclude = @recipInclude + N'IsArchived,';
                    IF COL_LENGTH('NotificationRecipients', 'DeliveryStatus') IS NOT NULL SET @recipInclude = @recipInclude + N'DeliveryStatus,';
                    IF LEN(@recipInclude) > 0
                    BEGIN
                        SET @recipInclude = LEFT(@recipInclude, LEN(@recipInclude) - 1);
                        EXEC(N'CREATE INDEX IX_NotificationRecipients_TenantId_UserId_NotificationId ON NotificationRecipients (TenantId, UserId, NotificationId) INCLUDE (' + @recipInclude + N')');
                    END
                    ELSE
                        CREATE INDEX IX_NotificationRecipients_TenantId_UserId_NotificationId ON NotificationRecipients (TenantId, UserId, NotificationId);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationDeliveryLogs_TenantId_NotificationId' AND object_id = OBJECT_ID('NotificationDeliveryLogs'))
                    CREATE INDEX IX_NotificationDeliveryLogs_TenantId_NotificationId
                    ON NotificationDeliveryLogs (TenantId, NotificationId, CreatedAt DESC);
                """, cancellationToken: cancellationToken));

            logger.LogInformation("NotificationTenantIsolationMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationTenantIsolationMigration failed.");
            throw;
        }
    }
}
