using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 9 Commands module — retry/audit fields on GpsDeviceCommands plus GpsCommandResponses
/// (device/Traccar ACK payloads, keyed to GpsDeviceCommands.Id).
/// </summary>
public static class GpsCommandsPhase9Migration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsDeviceCommands', 'Attributes') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD Attributes NVARCHAR(MAX) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'RetryCount') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD RetryCount INT NOT NULL CONSTRAINT DF_GpsDeviceCommands_RetryCount DEFAULT 0;
                IF COL_LENGTH('GpsDeviceCommands', 'MaxRetries') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD MaxRetries INT NOT NULL CONSTRAINT DF_GpsDeviceCommands_MaxRetries DEFAULT 3;
                IF COL_LENGTH('GpsDeviceCommands', 'NextRetryAt') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD NextRetryAt DATETIME2 NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'ErrorMessage') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD ErrorMessage NVARCHAR(500) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'TraccarCommandId') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD TraccarCommandId INT NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'CancelledAt') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD CancelledAt DATETIME2 NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'CancelledBy') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD CancelledBy NVARCHAR(100) NULL;
                IF COL_LENGTH('GpsDeviceCommands', 'UpdatedAt') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD UpdatedAt DATETIME2 NULL;
                """, cancellationToken: cancellationToken));

            // Device-level relay capability flag, mirroring the existing GpsDevices.SupportsEngineCutoff
            // pattern — the command handler's capability gate reads this column directly rather than
            // joining TrackerModels on every send.
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsDevices', 'SupportsRelay') IS NULL
                    ALTER TABLE GpsDevices ADD SupportsRelay BIT NOT NULL CONSTRAINT DF_GpsDevices_SupportsRelay DEFAULT 0;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE d
                SET d.SupportsRelay = m.SupportsRelay
                FROM GpsDevices d
                INNER JOIN TrackerModels m ON m.Id = d.TrackerModelId
                WHERE d.SupportsRelay = 0 AND m.SupportsRelay = 1;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsDeviceCommands_DeviceStatus')
                    CREATE INDEX IX_GpsDeviceCommands_DeviceStatus ON GpsDeviceCommands(GpsDeviceId, Status) WHERE IsDeleted = 0;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsDeviceCommands_Retry')
                    CREATE INDEX IX_GpsDeviceCommands_Retry ON GpsDeviceCommands(Status, NextRetryAt) WHERE IsDeleted = 0;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsCommandResponses')
                CREATE TABLE GpsCommandResponses (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    CommandId INT NOT NULL,
                    Source NVARCHAR(20) NOT NULL,
                    ResponseCode NVARCHAR(50) NULL,
                    ResponseText NVARCHAR(1000) NULL,
                    ReceivedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_GpsCommandResponses_Commands FOREIGN KEY (CommandId) REFERENCES GpsDeviceCommands(Id)
                );
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsCommandResponses_CommandId')
                    CREATE INDEX IX_GpsCommandResponses_CommandId ON GpsCommandResponses(CommandId);
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsCommandsPhase9Migration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsCommandsPhase9Migration failed.");
        }
    }
}
