using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsDeviceAssignmentMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF OBJECT_ID('GpsDeviceAssignments', 'U') IS NULL
                BEGIN
                    CREATE TABLE GpsDeviceAssignments (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TenantId INT NOT NULL,
                        GpsDeviceId INT NOT NULL,
                        VehicleId INT NOT NULL,
                        DriverId INT NULL,
                        InstalledDate DATETIME2 NOT NULL,
                        RemovedDate DATETIME2 NULL,
                        InstalledBy NVARCHAR(200) NULL,
                        RemovedBy NVARCHAR(200) NULL,
                        Reason NVARCHAR(500) NULL,
                        InstallationNotes NVARCHAR(1000) NULL,
                        RelayOutput NVARCHAR(50) NULL,
                        IsActive BIT NOT NULL CONSTRAINT DF_GpsDeviceAssignments_IsActive DEFAULT 0,
                        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GpsDeviceAssignments_CreatedAt DEFAULT GETUTCDATE(),
                        UpdatedAt DATETIME2 NULL,
                        CONSTRAINT FK_GpsDeviceAssignments_Device FOREIGN KEY (GpsDeviceId) REFERENCES GpsDevices(Id),
                        CONSTRAINT FK_GpsDeviceAssignments_Vehicle FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
                    );

                    CREATE INDEX IX_GpsDeviceAssignments_Device ON GpsDeviceAssignments(GpsDeviceId, InstalledDate DESC);
                    CREATE INDEX IX_GpsDeviceAssignments_Vehicle ON GpsDeviceAssignments(VehicleId, InstalledDate DESC);

                    CREATE UNIQUE INDEX UX_GpsDeviceAssignments_ActiveDevice
                        ON GpsDeviceAssignments(GpsDeviceId)
                        WHERE IsActive = 1;

                    CREATE UNIQUE INDEX UX_GpsDeviceAssignments_ActiveVehicle
                        ON GpsDeviceAssignments(VehicleId)
                        WHERE IsActive = 1;
                END

                -- Backfill active assignments from legacy GpsDevices rows
                INSERT INTO GpsDeviceAssignments (
                    TenantId, GpsDeviceId, VehicleId, DriverId, InstalledDate, InstalledBy,
                    InstallationNotes, RelayOutput, IsActive, CreatedAt)
                SELECT
                    COALESCE(d.TenantId, v.TenantId),
                    d.Id,
                    d.VehicleId,
                    d.DriverId,
                    COALESCE(d.InstallationDate, d.CreatedAt, GETUTCDATE()),
                    d.InstalledBy,
                    d.InstallationNotes,
                    d.RelayOutput,
                    1,
                    GETUTCDATE()
                FROM GpsDevices d
                INNER JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
                WHERE d.IsDeleted = 0
                  AND d.VehicleId IS NOT NULL
                  AND (d.CurrentStatus = 'Installed' OR d.CurrentStatus IS NULL)
                  AND NOT EXISTS (
                      SELECT 1 FROM GpsDeviceAssignments a
                      WHERE a.GpsDeviceId = d.Id AND a.IsActive = 1
                  );
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsDeviceAssignmentMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDeviceAssignmentMigration failed.");
            throw;
        }
    }
}
