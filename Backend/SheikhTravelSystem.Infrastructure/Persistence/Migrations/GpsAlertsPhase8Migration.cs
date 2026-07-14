using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 8 Alerts MVP — severity/status/audit fields on GpsAlertEvents plus
/// AlertSettings (per-user notification prefs) and AlertNotificationLogs (delivery audit).
/// </summary>
public static class GpsAlertsPhase8Migration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsAlertEvents', 'Severity') IS NULL
                    ALTER TABLE GpsAlertEvents ADD Severity NVARCHAR(20) NULL;
                IF COL_LENGTH('GpsAlertEvents', 'Status') IS NULL
                    ALTER TABLE GpsAlertEvents ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_GpsAlertEvents_Status DEFAULT 'active';
                IF COL_LENGTH('GpsAlertEvents', 'DriverId') IS NULL
                    ALTER TABLE GpsAlertEvents ADD DriverId INT NULL;
                IF COL_LENGTH('GpsAlertEvents', 'AcknowledgedAt') IS NULL
                    ALTER TABLE GpsAlertEvents ADD AcknowledgedAt DATETIME2 NULL;
                IF COL_LENGTH('GpsAlertEvents', 'AcknowledgedBy') IS NULL
                    ALTER TABLE GpsAlertEvents ADD AcknowledgedBy NVARCHAR(100) NULL;
                IF COL_LENGTH('GpsAlertEvents', 'ResolvedAt') IS NULL
                    ALTER TABLE GpsAlertEvents ADD ResolvedAt DATETIME2 NULL;
                IF COL_LENGTH('GpsAlertEvents', 'ResolvedBy') IS NULL
                    ALTER TABLE GpsAlertEvents ADD ResolvedBy NVARCHAR(100) NULL;
                IF COL_LENGTH('GpsAlertEvents', 'ResolutionNotes') IS NULL
                    ALTER TABLE GpsAlertEvents ADD ResolutionNotes NVARCHAR(1000) NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_GpsAlertEvents_Drivers')
                    ALTER TABLE GpsAlertEvents ADD CONSTRAINT FK_GpsAlertEvents_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id);
                """, cancellationToken: cancellationToken));

            // One-time backfill: pre-existing acknowledged rows never had AcknowledgedAt, so use it
            // as the "already processed" guard — once set, this UPDATE no longer matches those rows.
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE GpsAlertEvents
                SET Status = 'acknowledged', AcknowledgedAt = Timestamp
                WHERE IsAcknowledged = 1 AND AcknowledgedAt IS NULL AND Status = 'active';
                """, cancellationToken: cancellationToken));

            // Self-healing severity backfill: covers historic rows and any row inserted by a path
            // that hasn't been migrated onto GpsAlertWriter yet. Kept in sync with
            // GpsAlertWriter.SeverityFor — same buckets, keyed by the literal EventType values
            // actually written by the detectors (speed_exceeded, vehicle_offline, etc.).
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE GpsAlertEvents
                SET Severity = CASE
                    WHEN EventType IN ('sos', 'power_cut') THEN 'critical'
                    WHEN EventType IN ('speed_exceeded', 'geofence_exit', 'vehicle_offline', 'device_offline', 'alarm') THEN 'high'
                    WHEN EventType IN ('ignition_on', 'low_battery', 'gps_lost', 'low_fuel', 'geofence_enter') THEN 'medium'
                    WHEN EventType IN ('ignition_off', 'online', 'device_online', 'vehicle_online') THEN 'low'
                    ELSE 'medium'
                END
                WHERE Severity IS NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAlertEvents_Status')
                    CREATE INDEX IX_GpsAlertEvents_Status ON GpsAlertEvents(Status, Timestamp DESC) WHERE IsDeleted = 0;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAlertEvents_Severity')
                    CREATE INDEX IX_GpsAlertEvents_Severity ON GpsAlertEvents(Severity, Timestamp DESC) WHERE IsDeleted = 0;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAlertEvents_DriverId')
                    CREATE INDEX IX_GpsAlertEvents_DriverId ON GpsAlertEvents(DriverId) WHERE IsDeleted = 0 AND DriverId IS NOT NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AlertSettings')
                CREATE TABLE AlertSettings (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    AlertType NVARCHAR(50) NOT NULL,
                    InAppEnabled BIT NOT NULL DEFAULT 1,
                    EmailEnabled BIT NOT NULL DEFAULT 0,
                    PushEnabled BIT NOT NULL DEFAULT 0,
                    SmsEnabled BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    CONSTRAINT UQ_AlertSettings_User_Type UNIQUE (UserId, AlertType)
                );
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AlertNotificationLogs')
                CREATE TABLE AlertNotificationLogs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    AlertEventId INT NOT NULL,
                    Channel NVARCHAR(20) NOT NULL,
                    Recipient NVARCHAR(200) NULL,
                    Status NVARCHAR(20) NOT NULL,
                    SentAt DATETIME2 NULL,
                    Error NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_AlertNotificationLogs_Events FOREIGN KEY (AlertEventId) REFERENCES GpsAlertEvents(Id)
                );
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlertNotificationLogs_AlertEventId')
                    CREATE INDEX IX_AlertNotificationLogs_AlertEventId ON AlertNotificationLogs(AlertEventId);
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsAlertsPhase8Migration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsAlertsPhase8Migration failed.");
        }
    }
}
