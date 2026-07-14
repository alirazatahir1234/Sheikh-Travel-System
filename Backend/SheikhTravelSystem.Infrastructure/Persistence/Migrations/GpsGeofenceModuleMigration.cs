using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsGeofenceModuleMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('Geofences', 'Color') IS NULL
                    ALTER TABLE Geofences ADD Color NVARCHAR(20) NOT NULL CONSTRAINT DF_Geofences_Color DEFAULT '#0f766e';
                IF COL_LENGTH('Geofences', 'Category') IS NULL
                    ALTER TABLE Geofences ADD Category NVARCHAR(50) NULL;
                IF COL_LENGTH('Geofences', 'Description') IS NULL
                    ALTER TABLE Geofences ADD Description NVARCHAR(500) NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GeofenceAssignments')
                CREATE TABLE GeofenceAssignments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    GeofenceId INT NOT NULL,
                    VehicleId INT NULL,
                    BranchId INT NULL,
                    DepartmentId INT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    CreatedBy NVARCHAR(100) NULL,
                    UpdatedBy NVARCHAR(100) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_GeofenceAssignments_Geofences FOREIGN KEY (GeofenceId) REFERENCES Geofences(Id),
                    CONSTRAINT FK_GeofenceAssignments_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                    CONSTRAINT CK_GeofenceAssignments_OneScope CHECK (
                        (CASE WHEN VehicleId IS NOT NULL THEN 1 ELSE 0 END
                       + CASE WHEN BranchId IS NOT NULL THEN 1 ELSE 0 END
                       + CASE WHEN DepartmentId IS NOT NULL THEN 1 ELSE 0 END) = 1
                    )
                );
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GeofenceAssignments_Vehicle')
                    CREATE UNIQUE INDEX UX_GeofenceAssignments_Vehicle
                    ON GeofenceAssignments(GeofenceId, VehicleId)
                    WHERE IsDeleted = 0 AND VehicleId IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GeofenceAssignments_Branch')
                    CREATE UNIQUE INDEX UX_GeofenceAssignments_Branch
                    ON GeofenceAssignments(GeofenceId, BranchId)
                    WHERE IsDeleted = 0 AND BranchId IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GeofenceAssignments_Department')
                    CREATE UNIQUE INDEX UX_GeofenceAssignments_Department
                    ON GeofenceAssignments(GeofenceId, DepartmentId)
                    WHERE IsDeleted = 0 AND DepartmentId IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GeofenceAssignments_GeofenceId')
                    CREATE INDEX IX_GeofenceAssignments_GeofenceId
                    ON GeofenceAssignments(GeofenceId) WHERE IsDeleted = 0;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsGeofenceModuleMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsGeofenceModuleMigration failed.");
        }
    }
}
