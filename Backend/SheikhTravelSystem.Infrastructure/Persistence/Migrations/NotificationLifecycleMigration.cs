using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Separates Archive from Soft Delete, adds per-recipient lifecycle flags and retention metadata.
/// Backfills prior IsDeleted=1 rows as Archived (not trash).
/// </summary>
public static class NotificationLifecycleMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                -- Notifications: archive + retention
                IF COL_LENGTH('Notifications', 'IsArchived') IS NULL
                    ALTER TABLE Notifications ADD IsArchived BIT NOT NULL CONSTRAINT DF_Notifications_IsArchived DEFAULT 0;
                IF COL_LENGTH('Notifications', 'ArchivedAt') IS NULL
                    ALTER TABLE Notifications ADD ArchivedAt DATETIME2 NULL;
                IF COL_LENGTH('Notifications', 'RetentionCategory') IS NULL
                    ALTER TABLE Notifications ADD RetentionCategory NVARCHAR(40) NOT NULL CONSTRAINT DF_Notifications_RetentionCategory DEFAULT 'Standard';
                IF COL_LENGTH('Notifications', 'NeverAutoDelete') IS NULL
                    ALTER TABLE Notifications ADD NeverAutoDelete BIT NOT NULL CONSTRAINT DF_Notifications_NeverAutoDelete DEFAULT 0;

                -- Recipients: per-user lifecycle
                IF COL_LENGTH('NotificationRecipients', 'IsArchived') IS NULL
                    ALTER TABLE NotificationRecipients ADD IsArchived BIT NOT NULL CONSTRAINT DF_NotificationRecipients_IsArchived DEFAULT 0;
                IF COL_LENGTH('NotificationRecipients', 'ArchivedAt') IS NULL
                    ALTER TABLE NotificationRecipients ADD ArchivedAt DATETIME2 NULL;
                IF COL_LENGTH('NotificationRecipients', 'IsDeleted') IS NULL
                    ALTER TABLE NotificationRecipients ADD IsDeleted BIT NOT NULL CONSTRAINT DF_NotificationRecipients_IsDeleted DEFAULT 0;
                IF COL_LENGTH('NotificationRecipients', 'DeletedAt') IS NULL
                    ALTER TABLE NotificationRecipients ADD DeletedAt DATETIME2 NULL;
                IF COL_LENGTH('NotificationRecipients', 'DeletedBy') IS NULL
                    ALTER TABLE NotificationRecipients ADD DeletedBy INT NULL;
                """, cancellationToken: cancellationToken));

            // Backfill: former soft-delete (= archive UX) → IsArchived, clear IsDeleted
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Notifications
                SET IsArchived = 1,
                    ArchivedAt = ISNULL(UpdatedAt, CreatedAt),
                    IsDeleted = 0,
                    UpdatedAt = GETUTCDATE()
                WHERE IsDeleted = 1 AND ISNULL(IsArchived, 0) = 0;
                """, cancellationToken: cancellationToken));

            // Ensure recipient rows for owned notifications
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO NotificationRecipients (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, ArchivedAt, IsDeleted)
                SELECT n.Id, n.UserId,
                       ISNULL(n.DeliveryStatus, CASE WHEN n.IsSent = 1 THEN 'Sent' ELSE 'Pending' END),
                       n.IsRead, n.CreatedAt,
                       ISNULL(n.IsArchived, 0),
                       n.ArchivedAt,
                       0
                FROM Notifications n
                WHERE n.UserId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM NotificationRecipients r
                      WHERE r.NotificationId = n.Id AND r.UserId = n.UserId);
                """, cancellationToken: cancellationToken));

            // Sync archive flags onto recipient rows for previously "deleted" (= archived) notifications
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE r
                SET r.IsArchived = 1,
                    r.ArchivedAt = ISNULL(r.ArchivedAt, n.ArchivedAt),
                    r.IsDeleted = 0,
                    r.DeletedAt = NULL,
                    r.DeletedBy = NULL
                FROM NotificationRecipients r
                INNER JOIN Notifications n ON n.Id = r.NotificationId
                WHERE n.IsArchived = 1 AND ISNULL(r.IsArchived, 0) = 0;
                """, cancellationToken: cancellationToken));

            // Retention categories from type / module / template
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Notifications SET
                    RetentionCategory = CASE
                        WHEN Type = 6 OR TemplateKey IN ('sos_alert') OR Module = 'Security' THEN 'Critical'
                        WHEN Module = 'Compliance' OR TemplateKey LIKE '%compliance%' OR TemplateKey LIKE '%license%' OR TemplateKey LIKE '%insurance%' THEN 'Compliance'
                        WHEN Module = 'Fleet' AND TemplateKey IN ('speed_alert') THEN 'Operational'
                        WHEN Module = 'Fleet' OR TemplateKey IN ('vehicle_offline') OR Type = 3 THEN 'Operational'
                        WHEN Module IN ('Maintenance') THEN 'Maintenance'
                        WHEN Module = 'Communication' OR TemplateKey LIKE 'ai_%' THEN 'Ai'
                        WHEN Module = 'Security' THEN 'Security'
                        ELSE ISNULL(NULLIF(RetentionCategory, ''), 'Standard')
                    END,
                    NeverAutoDelete = CASE
                        WHEN Type = 6 OR TemplateKey IN ('sos_alert') OR Title LIKE '%SOS%' OR Title LIKE '%Panic%'
                             OR Title LIKE '%Theft%' OR Title LIKE '%Accident%' THEN 1
                        ELSE ISNULL(NeverAutoDelete, 0)
                    END
                WHERE ISNULL(RetentionCategory, 'Standard') = 'Standard' OR NeverAutoDelete = 0;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationRecipients_UserLifecycle' AND object_id = OBJECT_ID('NotificationRecipients'))
                    CREATE INDEX IX_NotificationRecipients_UserLifecycle
                    ON NotificationRecipients (UserId, IsDeleted, IsArchived, IsRead)
                    INCLUDE (NotificationId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationRecipients_NotificationUser' AND object_id = OBJECT_ID('NotificationRecipients'))
                    CREATE INDEX IX_NotificationRecipients_NotificationUser
                    ON NotificationRecipients (NotificationId, UserId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_Retention' AND object_id = OBJECT_ID('Notifications'))
                    CREATE INDEX IX_Notifications_Retention
                    ON Notifications (RetentionCategory, NeverAutoDelete, IsArchived, IsDeleted, CreatedAt);
                """, cancellationToken: cancellationToken));

            // Seed default retention settings for tenant 1 (idempotent)
            await connection.ExecuteAsync(new CommandDefinition("""
                DECLARE @TenantId INT = 1;
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformSettings')
                BEGIN
                    ;WITH Defaults AS (
                        SELECT * FROM (VALUES
                            ('ReadArchiveDays', '30'),
                            ('ArchivedDeleteDays', '180'),
                            ('FailedDeleteDays', '90'),
                            ('DraftDeleteDays', '30'),
                            ('OperationalDeleteDays', '90'),
                            ('MaintenanceDeleteDays', '730'),
                            ('ComplianceDeleteDays', '2555'),
                            ('CriticalNeverDelete', 'true'),
                            ('SecurityDeleteDays', '730')
                        ) v([Key], Value)
                    )
                    INSERT INTO PlatformSettings (TenantId, Category, [Key], Value)
                    SELECT @TenantId, 'NotificationRetention', d.[Key], d.Value
                    FROM Defaults d
                    WHERE NOT EXISTS (
                        SELECT 1 FROM PlatformSettings ps
                        WHERE ps.TenantId = @TenantId AND ps.Category = 'NotificationRetention' AND ps.[Key] = d.[Key]);
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("NotificationLifecycleMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationLifecycleMigration failed.");
            throw;
        }
    }
}
