using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 14 completion — Module/ReadDate, recipients, preferences, retry fields, template metadata.
/// </summary>
public static class NotificationCenterV2Migration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('Notifications', 'Module') IS NULL
                    ALTER TABLE Notifications ADD Module NVARCHAR(60) NULL;
                IF COL_LENGTH('Notifications', 'ReadDate') IS NULL
                    ALTER TABLE Notifications ADD ReadDate DATETIME2 NULL;
                IF COL_LENGTH('Notifications', 'RetryCount') IS NULL
                    ALTER TABLE Notifications ADD RetryCount INT NOT NULL CONSTRAINT DF_Notifications_RetryCount DEFAULT 0;
                IF COL_LENGTH('Notifications', 'NextRetryAt') IS NULL
                    ALTER TABLE Notifications ADD NextRetryAt DATETIME2 NULL;
                IF COL_LENGTH('Notifications', 'DeliveryStatus') IS NULL
                    ALTER TABLE Notifications ADD DeliveryStatus NVARCHAR(40) NULL;

                IF COL_LENGTH('NotificationTemplates', 'Language') IS NULL
                    ALTER TABLE NotificationTemplates ADD Language NVARCHAR(20) NOT NULL CONSTRAINT DF_NotificationTemplates_Language DEFAULT 'en';
                IF COL_LENGTH('NotificationTemplates', 'Variables') IS NULL
                    ALTER TABLE NotificationTemplates ADD Variables NVARCHAR(MAX) NULL;

                IF COL_LENGTH('NotificationDeliveryLogs', 'Provider') IS NULL
                    ALTER TABLE NotificationDeliveryLogs ADD Provider NVARCHAR(80) NULL;
                IF COL_LENGTH('NotificationDeliveryLogs', 'RetryCount') IS NULL
                    ALTER TABLE NotificationDeliveryLogs ADD RetryCount INT NOT NULL CONSTRAINT DF_NotificationDeliveryLogs_RetryCount DEFAULT 0;
                IF COL_LENGTH('NotificationDeliveryLogs', 'NextRetryAt') IS NULL
                    ALTER TABLE NotificationDeliveryLogs ADD NextRetryAt DATETIME2 NULL;

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationRecipients')
                CREATE TABLE NotificationRecipients (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    NotificationId INT NOT NULL,
                    UserId INT NOT NULL,
                    DeliveryStatus NVARCHAR(40) NOT NULL DEFAULT 'Pending',
                    IsRead BIT NOT NULL DEFAULT 0,
                    ReadAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_NotificationRecipients_Notifications
                        FOREIGN KEY (NotificationId) REFERENCES Notifications(Id),
                    CONSTRAINT FK_NotificationRecipients_Users
                        FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationPreferences')
                CREATE TABLE NotificationPreferences (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    EmailEnabled BIT NOT NULL DEFAULT 1,
                    SmsEnabled BIT NOT NULL DEFAULT 1,
                    PushEnabled BIT NOT NULL DEFAULT 1,
                    BrowserEnabled BIT NOT NULL DEFAULT 1,
                    WhatsAppEnabled BIT NOT NULL DEFAULT 0,
                    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT UQ_NotificationPreferences_UserId UNIQUE (UserId),
                    CONSTRAINT FK_NotificationPreferences_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_Module' AND object_id = OBJECT_ID('Notifications'))
                    CREATE INDEX IX_Notifications_Module ON Notifications (Module) WHERE IsDeleted = 0;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_NextRetryAt' AND object_id = OBJECT_ID('Notifications'))
                    CREATE INDEX IX_Notifications_NextRetryAt ON Notifications (NextRetryAt, IsSent)
                    WHERE IsDeleted = 0 AND IsSent = 0;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationRecipients_UserId' AND object_id = OBJECT_ID('NotificationRecipients'))
                    CREATE INDEX IX_NotificationRecipients_UserId ON NotificationRecipients (UserId, IsRead);
                """, cancellationToken: cancellationToken));

            await SeedExtraTemplatesAsync(connection, cancellationToken);

            logger.LogInformation("NotificationCenterV2Migration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationCenterV2Migration failed.");
            throw;
        }
    }

    private static async Task SeedExtraTemplatesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var templates = new (string Key, string Name, string Subject, string Body, string Channel, string Variables)[]
        {
            ("sos_alert", "SOS Alert", "SOS: {{title}}", "{{message}}", "InApp", "[\"title\",\"message\"]"),
            ("sos_alert", "SOS Alert", "SOS: {{title}}", "{{message}}", "Sms", "[\"title\",\"message\"]"),
            ("speed_alert", "Speed Alert", "Overspeed: {{title}}", "{{message}}", "InApp", "[\"title\",\"message\"]"),
            ("booking_created", "Booking Created", "{{title}}", "{{message}}", "InApp", "[\"title\",\"message\"]"),
            ("booking_created", "Booking Created", "{{title}}", "{{message}}", "Email", "[\"title\",\"message\"]"),
            ("payment_received", "Payment Received", "{{title}}", "{{message}}", "InApp", "[\"title\",\"message\"]"),
            ("payment_received", "Payment Received", "{{title}}", "{{message}}", "Email", "[\"title\",\"message\"]"),
            ("compliance_reminder", "Compliance Reminder", "{{title}}", "{{message}}", "InApp", "[\"title\",\"message\"]"),
            ("compliance_reminder", "Compliance Reminder", "{{title}}", "{{message}}", "Email", "[\"title\",\"message\"]"),
        };

        foreach (var t in templates)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (
                    SELECT 1 FROM NotificationTemplates
                    WHERE TemplateKey = @Key AND Channel = @Channel AND IsDeleted = 0)
                INSERT INTO NotificationTemplates
                    (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
                VALUES (@Key, @Name, @Subject, @Body, @Channel, 1, 'en', @Variables, GETUTCDATE(), 0);
                """,
                new { t.Key, t.Name, t.Subject, t.Body, t.Channel, t.Variables },
                cancellationToken: ct));
        }
    }
}
