using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// SheikhGo AI Platform schema — device tokens, decision audit, escalation,
/// digests, recommendations, feature store, AI config/usage.
/// </summary>
public static class AiPlatformMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserDeviceTokens')
                CREATE TABLE UserDeviceTokens (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    Token NVARCHAR(512) NOT NULL,
                    Platform NVARCHAR(40) NOT NULL DEFAULT 'android',
                    AppName NVARCHAR(40) NOT NULL DEFAULT 'driver',
                    LastSeenAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    CONSTRAINT UQ_UserDeviceTokens_Token UNIQUE (Token),
                    CONSTRAINT FK_UserDeviceTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserPresence')
                CREATE TABLE UserPresence (
                    UserId INT NOT NULL PRIMARY KEY,
                    BrowserOnline BIT NOT NULL DEFAULT 0,
                    MobileOnline BIT NOT NULL DEFAULT 0,
                    LastBrowserAt DATETIME2 NULL,
                    LastMobileAt DATETIME2 NULL,
                    LastLoginAt DATETIME2 NULL,
                    LastReadAt DATETIME2 NULL,
                    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_UserPresence_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiDecisionAudit')
                CREATE TABLE AiDecisionAudit (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NULL,
                    EventType NVARCHAR(80) NOT NULL,
                    ReferenceType NVARCHAR(60) NULL,
                    ReferenceId INT NULL,
                    Decision NVARCHAR(40) NOT NULL,
                    Reason NVARCHAR(500) NULL,
                    Priority INT NOT NULL DEFAULT 2,
                    ChannelsJson NVARCHAR(400) NULL,
                    CooldownKey NVARCHAR(200) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiCooldownRules')
                CREATE TABLE AiCooldownRules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NULL,
                    EventType NVARCHAR(80) NOT NULL,
                    CooldownMinutes INT NOT NULL DEFAULT 5,
                    MinPriority INT NOT NULL DEFAULT 1,
                    CorrelateWith NVARCHAR(200) NULL,
                    ChannelsJson NVARCHAR(400) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EscalationRules')
                CREATE TABLE EscalationRules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NULL,
                    EventType NVARCHAR(80) NOT NULL,
                    LevelOrder INT NOT NULL,
                    TargetRole NVARCHAR(80) NOT NULL,
                    TimeoutMinutes INT NOT NULL DEFAULT 15,
                    Channel NVARCHAR(40) NOT NULL DEFAULT 'InApp',
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EscalationState')
                CREATE TABLE EscalationState (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    NotificationId INT NULL,
                    AlertEventId INT NULL,
                    EventType NVARCHAR(80) NOT NULL,
                    ReferenceId INT NULL,
                    CurrentLevel INT NOT NULL DEFAULT 0,
                    Status NVARCHAR(40) NOT NULL DEFAULT 'Pending',
                    NextEscalateAt DATETIME2 NULL,
                    AcknowledgedAt DATETIME2 NULL,
                    AcknowledgedByUserId INT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FleetHealthSnapshots')
                CREATE TABLE FleetHealthSnapshots (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    HealthPercent DECIMAL(5,2) NOT NULL,
                    GpsOnlineRate DECIMAL(5,2) NULL,
                    MaintenanceScore DECIMAL(5,2) NULL,
                    ComplianceScore DECIMAL(5,2) NULL,
                    DriverScore DECIMAL(5,2) NULL,
                    CriticalAlerts INT NOT NULL DEFAULT 0,
                    DetailsJson NVARCHAR(MAX) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiDigests')
                CREATE TABLE AiDigests (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    UserId INT NULL,
                    DigestType NVARCHAR(40) NOT NULL DEFAULT 'Morning',
                    Title NVARCHAR(200) NOT NULL,
                    Body NVARCHAR(MAX) NOT NULL,
                    StatsJson NVARCHAR(MAX) NULL,
                    SentAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiRecommendations')
                CREATE TABLE AiRecommendations (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    EntityType NVARCHAR(60) NOT NULL,
                    EntityId INT NOT NULL,
                    Category NVARCHAR(60) NOT NULL,
                    Severity NVARCHAR(20) NOT NULL DEFAULT 'Medium',
                    Title NVARCHAR(200) NOT NULL,
                    Action NVARCHAR(500) NOT NULL,
                    Source NVARCHAR(40) NOT NULL DEFAULT 'Rule',
                    Score DECIMAL(5,2) NULL,
                    ExpiresAt DATETIME2 NULL,
                    IsDismissed BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiFeatureSnapshots')
                CREATE TABLE AiFeatureSnapshots (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    EntityType NVARCHAR(60) NOT NULL,
                    EntityId INT NOT NULL,
                    FeatureSet NVARCHAR(60) NOT NULL,
                    FeaturesJson NVARCHAR(MAX) NOT NULL,
                    CapturedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiPredictions')
                CREATE TABLE AiPredictions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    EntityType NVARCHAR(60) NOT NULL,
                    EntityId INT NOT NULL,
                    PredictionType NVARCHAR(60) NOT NULL,
                    Probability DECIMAL(5,2) NOT NULL,
                    ExpectedDays INT NULL,
                    Label NVARCHAR(200) NULL,
                    ModelVersion NVARCHAR(40) NOT NULL DEFAULT 'heuristic-v1',
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiProviderConfig')
                CREATE TABLE AiProviderConfig (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Provider NVARCHAR(40) NOT NULL DEFAULT 'None',
                    IsEnabled BIT NOT NULL DEFAULT 0,
                    CopilotEnabled BIT NOT NULL DEFAULT 0,
                    DecisionEngineEnabled BIT NOT NULL DEFAULT 1,
                    DigestEnabled BIT NOT NULL DEFAULT 1,
                    PredictionsEnabled BIT NOT NULL DEFAULT 1,
                    MonthlyBudgetUsd DECIMAL(10,2) NULL,
                    SoftTokenLimit INT NULL,
                    ApiEndpoint NVARCHAR(500) NULL,
                    ModelName NVARCHAR(120) NULL,
                    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT UQ_AiProviderConfig_Tenant UNIQUE (TenantId)
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiUsageLedger')
                CREATE TABLE AiUsageLedger (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Feature NVARCHAR(80) NOT NULL,
                    Provider NVARCHAR(40) NOT NULL,
                    TokensUsed INT NOT NULL DEFAULT 0,
                    CostUsd DECIMAL(10,4) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiLearningSignals')
                CREATE TABLE AiLearningSignals (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    UserId INT NOT NULL,
                    EventType NVARCHAR(80) NOT NULL,
                    Action NVARCHAR(40) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AiDecisionAudit_CooldownKey' AND object_id = OBJECT_ID('AiDecisionAudit'))
                    CREATE INDEX IX_AiDecisionAudit_CooldownKey ON AiDecisionAudit (CooldownKey, CreatedAt DESC);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EscalationState_Next' AND object_id = OBJECT_ID('EscalationState'))
                    CREATE INDEX IX_EscalationState_Next ON EscalationState (Status, NextEscalateAt)
                    WHERE Status = 'Pending';
                """, cancellationToken: cancellationToken));

            await SeedDefaultCooldownRulesAsync(connection, cancellationToken);
            await SeedDefaultEscalationAsync(connection, cancellationToken);

            logger.LogInformation("AiPlatformMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AiPlatformMigration failed.");
            throw;
        }
    }

    private static async Task SeedDefaultCooldownRulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var rules = new (string EventType, int Cooldown, int MinPriority, string Channels)[]
        {
            ("sos", 1, 4, "[\"InApp\",\"Browser\",\"Push\",\"Sms\"]"),
            ("vehicle_offline", 30, 3, "[\"InApp\",\"Browser\",\"Push\"]"),
            ("speed_exceeded", 15, 3, "[\"InApp\",\"Browser\",\"Push\"]"),
            ("low_fuel", 60, 2, "[\"InApp\",\"Browser\"]"),
            ("low_battery", 60, 2, "[\"InApp\",\"Browser\"]"),
            ("booking_created", 0, 2, "[\"InApp\",\"Browser\",\"Email\"]"),
            ("payment_received", 0, 2, "[\"InApp\",\"Browser\",\"Email\"]"),
            ("compliance_reminder", 1440, 3, "[\"InApp\",\"Email\"]"),
        };

        foreach (var r in rules)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM AiCooldownRules WHERE EventType = @EventType AND TenantId IS NULL)
                INSERT INTO AiCooldownRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                VALUES (NULL, @EventType, @Cooldown, @MinPriority, @Channels, 1)
                """,
                new { r.EventType, r.Cooldown, r.MinPriority, r.Channels },
                cancellationToken: ct));
        }
    }

    private static async Task SeedDefaultEscalationAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var levels = new (string EventType, int Level, string Role, int Timeout, string Channel)[]
        {
            ("sos", 1, "Dispatcher", 5, "InApp"),
            ("sos", 2, "FleetManager", 10, "Push"),
            ("sos", 3, "OperationsManager", 15, "Email"),
            ("sos", 4, "Owner", 20, "Sms"),
            ("vehicle_offline", 1, "Dispatcher", 30, "InApp"),
            ("vehicle_offline", 2, "FleetManager", 60, "Email"),
        };

        foreach (var l in levels)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (
                    SELECT 1 FROM EscalationRules
                    WHERE EventType = @EventType AND LevelOrder = @Level AND TenantId IS NULL)
                INSERT INTO EscalationRules (TenantId, EventType, LevelOrder, TargetRole, TimeoutMinutes, Channel, IsActive)
                VALUES (NULL, @EventType, @Level, @Role, @Timeout, @Channel, 1)
                """,
                new { l.EventType, l.Level, l.Role, l.Timeout, l.Channel },
                cancellationToken: ct));
        }
    }
}
