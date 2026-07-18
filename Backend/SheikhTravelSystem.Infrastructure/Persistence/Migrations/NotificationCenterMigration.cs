using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 14 — Notification Center: channel columns, templates, and delivery logs.
/// </summary>
public static class NotificationCenterMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('Notifications', 'Priority') IS NULL
                    ALTER TABLE Notifications ADD Priority INT NOT NULL CONSTRAINT DF_Notifications_Priority DEFAULT 2;
                IF COL_LENGTH('Notifications', 'Channel') IS NULL
                    ALTER TABLE Notifications ADD Channel NVARCHAR(40) NOT NULL CONSTRAINT DF_Notifications_Channel DEFAULT 'InApp';
                IF COL_LENGTH('Notifications', 'RecipientType') IS NULL
                    ALTER TABLE Notifications ADD RecipientType NVARCHAR(40) NULL;
                IF COL_LENGTH('Notifications', 'IsSent') IS NULL
                    ALTER TABLE Notifications ADD IsSent BIT NOT NULL CONSTRAINT DF_Notifications_IsSent DEFAULT 0;
                IF COL_LENGTH('Notifications', 'SentDate') IS NULL
                    ALTER TABLE Notifications ADD SentDate DATETIME2 NULL;
                IF COL_LENGTH('Notifications', 'TemplateKey') IS NULL
                    ALTER TABLE Notifications ADD TemplateKey NVARCHAR(100) NULL;

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationTemplates')
                CREATE TABLE NotificationTemplates (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TemplateKey NVARCHAR(100) NOT NULL,
                    TemplateName NVARCHAR(200) NOT NULL,
                    Subject NVARCHAR(300) NOT NULL,
                    Body NVARCHAR(MAX) NOT NULL,
                    Channel NVARCHAR(40) NOT NULL DEFAULT 'InApp',
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CONSTRAINT UQ_NotificationTemplates_Key_Channel UNIQUE (TemplateKey, Channel)
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationDeliveryLogs')
                CREATE TABLE NotificationDeliveryLogs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    NotificationId INT NOT NULL,
                    Channel NVARCHAR(40) NOT NULL,
                    Status NVARCHAR(40) NOT NULL,
                    Response NVARCHAR(MAX) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_NotificationDeliveryLogs_Notifications
                        FOREIGN KEY (NotificationId) REFERENCES Notifications(Id)
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_UserId_IsRead' AND object_id = OBJECT_ID('Notifications'))
                    CREATE INDEX IX_Notifications_UserId_IsRead ON Notifications (UserId, IsRead) WHERE IsDeleted = 0;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationDeliveryLogs_NotificationId' AND object_id = OBJECT_ID('NotificationDeliveryLogs'))
                    CREATE INDEX IX_NotificationDeliveryLogs_NotificationId ON NotificationDeliveryLogs (NotificationId, CreatedAt DESC);
                """, cancellationToken: cancellationToken));

            await SeedTemplatesAsync(connection, cancellationToken);
            logger.LogInformation("NotificationCenterMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationCenterMigration failed.");
            throw;
        }
    }

    private static async Task SeedTemplatesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var templates = new (string Key, string Name, string Subject, string Body, string Channel)[]
        {
            ("booking_confirmation", "Booking Confirmation", "Booking confirmed: {{title}}", "{{message}}", "Email"),
            ("booking_confirmation", "Booking Confirmation", "Booking confirmed: {{title}}", "{{message}}", "InApp"),
            ("driver_assigned", "Driver Assigned", "Driver assigned", "{{message}}", "InApp"),
            ("driver_assigned", "Driver Assigned", "Driver assigned", "{{message}}", "Push"),
            ("vehicle_offline", "Vehicle Offline", "Vehicle offline alert", "{{message}}", "InApp"),
            ("vehicle_offline", "Vehicle Offline", "Vehicle offline alert", "{{message}}", "Email"),
            ("fuel_alert", "Fuel Alert", "Fuel alert", "{{message}}", "InApp"),
            ("over_speed", "Over Speed", "Over-speed alert", "{{message}}", "InApp"),
            ("geofence_exit", "Geofence Exit", "Geofence exit", "{{message}}", "InApp"),
            ("trip_completed", "Trip Completed", "Trip completed", "{{message}}", "InApp"),
            ("trip_completed", "Trip Completed", "Trip completed", "{{message}}", "WhatsApp"),
            ("maintenance_reminder", "Maintenance Reminder", "Maintenance due", "{{message}}", "Sms"),
            ("otp", "OTP", "Your verification code", "{{message}}", "Sms"),
        };

        foreach (var t in templates)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (
                    SELECT 1 FROM NotificationTemplates
                    WHERE TemplateKey = @Key AND Channel = @Channel AND IsDeleted = 0)
                INSERT INTO NotificationTemplates (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, CreatedAt, IsDeleted)
                VALUES (@Key, @Name, @Subject, @Body, @Channel, 1, GETUTCDATE(), 0);
                """,
                new { t.Key, t.Name, t.Subject, t.Body, t.Channel },
                cancellationToken: ct));
        }
    }
}
