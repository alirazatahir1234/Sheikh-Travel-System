using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Links maintenance records/schedules to fleet zones (Geofences assessment schema).
/// Zone columns map to <c>Geofences</c>: Id, Name, Category, AreaType.
/// Idempotent — safe to re-run. ALTERs and indexes are separate batches (SQL Server
/// compiles each batch against the current schema).
/// </summary>
public static class MaintenanceZoneLinkMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Batch 1: add columns (must commit before indexes/FKs reference them).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('dbo.Maintenance', 'GeofenceId') IS NULL
                ALTER TABLE dbo.Maintenance ADD GeofenceId INT NULL;
            IF COL_LENGTH('dbo.Maintenance', 'ZoneName') IS NULL
                ALTER TABLE dbo.Maintenance ADD ZoneName NVARCHAR(200) NULL;
            IF COL_LENGTH('dbo.Maintenance', 'ZoneCategory') IS NULL
                ALTER TABLE dbo.Maintenance ADD ZoneCategory NVARCHAR(50) NULL;
            IF COL_LENGTH('dbo.Maintenance', 'ZoneAreaType') IS NULL
                ALTER TABLE dbo.Maintenance ADD ZoneAreaType NVARCHAR(20) NULL;

            IF COL_LENGTH('dbo.VehicleMaintenanceSchedules', 'GeofenceId') IS NULL
                ALTER TABLE dbo.VehicleMaintenanceSchedules ADD GeofenceId INT NULL;
            IF COL_LENGTH('dbo.VehicleMaintenanceSchedules', 'ZoneName') IS NULL
                ALTER TABLE dbo.VehicleMaintenanceSchedules ADD ZoneName NVARCHAR(200) NULL;
            IF COL_LENGTH('dbo.VehicleMaintenanceSchedules', 'ZoneCategory') IS NULL
                ALTER TABLE dbo.VehicleMaintenanceSchedules ADD ZoneCategory NVARCHAR(50) NULL;
            IF COL_LENGTH('dbo.VehicleMaintenanceSchedules', 'ZoneAreaType') IS NULL
                ALTER TABLE dbo.VehicleMaintenanceSchedules ADD ZoneAreaType NVARCHAR(20) NULL;
            """, cancellationToken: cancellationToken));

        // Batch 2: FKs (after columns exist).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Maintenance_Geofences')
               AND OBJECT_ID(N'dbo.Geofences', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.Maintenance', 'GeofenceId') IS NOT NULL
                ALTER TABLE dbo.Maintenance
                    ADD CONSTRAINT FK_Maintenance_Geofences
                    FOREIGN KEY (GeofenceId) REFERENCES dbo.Geofences(Id);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VehicleMaintenanceSchedules_Geofences')
               AND OBJECT_ID(N'dbo.Geofences', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.VehicleMaintenanceSchedules', 'GeofenceId') IS NOT NULL
                ALTER TABLE dbo.VehicleMaintenanceSchedules
                    ADD CONSTRAINT FK_VehicleMaintenanceSchedules_Geofences
                    FOREIGN KEY (GeofenceId) REFERENCES dbo.Geofences(Id);
            """, cancellationToken: cancellationToken));

        // Batch 3: indexes.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Maintenance_GeofenceId')
               AND COL_LENGTH('dbo.Maintenance', 'GeofenceId') IS NOT NULL
                CREATE INDEX IX_Maintenance_GeofenceId ON dbo.Maintenance(GeofenceId)
                    WHERE GeofenceId IS NOT NULL AND IsDeleted = 0;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleMaintenanceSchedules_GeofenceId')
               AND COL_LENGTH('dbo.VehicleMaintenanceSchedules', 'GeofenceId') IS NOT NULL
                CREATE INDEX IX_VehicleMaintenanceSchedules_GeofenceId
                    ON dbo.VehicleMaintenanceSchedules(GeofenceId)
                    WHERE GeofenceId IS NOT NULL AND IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "MaintenanceZoneLinkMigration applied (GeofenceId/ZoneName/ZoneCategory/ZoneAreaType on Maintenance + VehicleMaintenanceSchedules).");
    }
}
