using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Trips Phase 2: expenses, documents, passengers + trip notification templates.
/// </summary>
public static class TripsPhase2Migration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TripExpenses')
            CREATE TABLE TripExpenses (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TripId INT NOT NULL,
                ExpenseType NVARCHAR(40) NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                Description NVARCHAR(500) NULL,
                ExpenseDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CreatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_TripExpenses_Trips FOREIGN KEY (TripId) REFERENCES Trips(Id)
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripExpenses_TripId' AND object_id = OBJECT_ID('TripExpenses'))
                CREATE INDEX IX_TripExpenses_TripId ON TripExpenses (TripId) WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TripDocuments')
            CREATE TABLE TripDocuments (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TripId INT NOT NULL,
                DocumentType NVARCHAR(60) NOT NULL,
                FileName NVARCHAR(260) NOT NULL,
                StorageKey NVARCHAR(500) NOT NULL,
                UploadedBy NVARCHAR(100) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_TripDocuments_Trips FOREIGN KEY (TripId) REFERENCES Trips(Id)
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripDocuments_TripId' AND object_id = OBJECT_ID('TripDocuments'))
                CREATE INDEX IX_TripDocuments_TripId ON TripDocuments (TripId) WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TripPassengers')
            CREATE TABLE TripPassengers (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TripId INT NOT NULL,
                FullName NVARCHAR(200) NOT NULL,
                Phone NVARCHAR(40) NULL,
                BoardingStatus NVARCHAR(40) NOT NULL DEFAULT N'Pending',
                DropStatus NVARCHAR(40) NOT NULL DEFAULT N'Pending',
                Notes NVARCHAR(500) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_TripPassengers_Trips FOREIGN KEY (TripId) REFERENCES Trips(Id)
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripPassengers_TripId' AND object_id = OBJECT_ID('TripPassengers'))
                CREATE INDEX IX_TripPassengers_TripId ON TripPassengers (TripId) WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        // Seed InApp/Email templates for trip lifecycle events (idempotent).
        var templates = new (string Key, string Name, string Channel)[]
        {
            ("trip_driver_assigned", "Trip Driver Assigned", "InApp"),
            ("trip_driver_assigned", "Trip Driver Assigned", "Email"),
            ("trip_started", "Trip Started", "InApp"),
            ("trip_started", "Trip Started", "Email"),
            ("trip_completed", "Trip Completed", "InApp"),
            ("trip_completed", "Trip Completed", "Email"),
        };

        foreach (var (key, name, channel) in templates)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationTemplates')
                AND NOT EXISTS (
                    SELECT 1 FROM NotificationTemplates
                    WHERE TemplateKey = @Key AND Channel = @Channel AND IsDeleted = 0)
                INSERT INTO NotificationTemplates
                    (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
                VALUES (@Key, @Name, N'{{title}}', N'{{message}}', @Channel, 1, 'en', N'["title","message"]', GETUTCDATE(), 0);
                """,
                new { Key = key, Name = name, Channel = channel },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiDecisionRules')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM AiDecisionRules WHERE EventType = N'trip_driver_assigned' AND TenantId IS NULL)
                    INSERT INTO AiDecisionRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                    VALUES (NULL, N'trip_driver_assigned', 0, 2, N'["InApp","Browser","Email"]', 1);
                IF NOT EXISTS (SELECT 1 FROM AiDecisionRules WHERE EventType = N'trip_started' AND TenantId IS NULL)
                    INSERT INTO AiDecisionRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                    VALUES (NULL, N'trip_started', 0, 2, N'["InApp","Browser","Email"]', 1);
                IF NOT EXISTS (SELECT 1 FROM AiDecisionRules WHERE EventType = N'trip_completed' AND TenantId IS NULL)
                    INSERT INTO AiDecisionRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                    VALUES (NULL, N'trip_completed', 0, 2, N'["InApp","Browser","Email"]', 1);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("TripsPhase2Migration applied successfully.");
    }
}
