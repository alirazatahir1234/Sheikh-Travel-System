using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Driver fatigue alert events (signal ingest → ack window → auto-stop escalation) + per-tenant settings.
/// </summary>
public static class FatigueDetectionMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FatigueTenantSettings')
            BEGIN
                CREATE TABLE FatigueTenantSettings (
                    TenantId              INT NOT NULL PRIMARY KEY,
                    AckWindowSeconds      INT NOT NULL DEFAULT 30,
                    StageIntervalSeconds  INT NOT NULL DEFAULT 15,
                    AutoStopEnabled       BIT NOT NULL DEFAULT 1,
                    UpdatedAt             DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_FatigueTenantSettings_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
                );
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverFatigueEvents')
            BEGIN
                CREATE TABLE DriverFatigueEvents (
                    Id                  INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId            INT NOT NULL,
                    DriverId            INT NOT NULL,
                    VehicleId           INT NULL,
                    BookingId           INT NULL,
                    Source              NVARCHAR(40) NOT NULL DEFAULT N'external_api',
                    SignalType          NVARCHAR(40) NOT NULL,
                    Confidence          DECIMAL(5,4) NULL,
                    EarScore            DECIMAL(6,4) NULL,
                    ExpressionHint      NVARCHAR(80) NULL,
                    Latitude            FLOAT NULL,
                    Longitude           FLOAT NULL,
                    Status              NVARCHAR(30) NOT NULL DEFAULT N'AwaitingAck',
                    EscalationStage     INT NOT NULL DEFAULT 0,
                    AckDeadlineUtc      DATETIME2 NOT NULL,
                    NextEscalationUtc   DATETIME2 NULL,
                    AcknowledgedAtUtc   DATETIME2 NULL,
                    PayloadJson         NVARCHAR(MAX) NULL,
                    LastEscalationNote  NVARCHAR(500) NULL,
                    CreatedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    ResolvedAt          DATETIME2 NULL,
                    ResolvedBy          NVARCHAR(100) NULL,
                    IsDeleted           BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_DriverFatigueEvents_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_DriverFatigueEvents_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
                );
                CREATE INDEX IX_DriverFatigueEvents_Tenant_Status ON DriverFatigueEvents (TenantId, Status, CreatedAt DESC) WHERE IsDeleted = 0;
                CREATE INDEX IX_DriverFatigueEvents_Driver_Open ON DriverFatigueEvents (TenantId, DriverId, Status) WHERE IsDeleted = 0;
                CREATE INDEX IX_DriverFatigueEvents_Escalation ON DriverFatigueEvents (Status, NextEscalationUtc) WHERE IsDeleted = 0 AND Status = N'Escalating';
            END

            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverFatigueEvents')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverFatigueEvents' AND COLUMN_NAME = 'DriverRiskScore')
                    ALTER TABLE DriverFatigueEvents ADD DriverRiskScore DECIMAL(6,4) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverFatigueEvents' AND COLUMN_NAME = 'CompositeRiskScore')
                    ALTER TABLE DriverFatigueEvents ADD CompositeRiskScore DECIMAL(6,4) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverFatigueEvents' AND COLUMN_NAME = 'SupportingSignalsJson')
                    ALTER TABLE DriverFatigueEvents ADD SupportingSignalsJson NVARCHAR(MAX) NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Fatigue detection schema migration completed.");
    }
}
