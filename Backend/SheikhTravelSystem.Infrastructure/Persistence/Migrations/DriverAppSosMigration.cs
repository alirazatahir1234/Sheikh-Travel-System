using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Driver app SOS alerts + driver-app attendance columns used by check-in/out.
/// Idempotent — safe to re-run.
/// </summary>
public static class DriverAppSosMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverSosAlerts')
            BEGIN
                CREATE TABLE DriverSosAlerts (
                    Id          INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId    INT NOT NULL,
                    DriverId    INT NOT NULL,
                    VehicleId   INT NULL,
                    BookingId   INT NULL,
                    Latitude    FLOAT NULL,
                    Longitude   FLOAT NULL,
                    Message     NVARCHAR(500) NULL,
                    Status      NVARCHAR(20) NOT NULL DEFAULT N'Open',
                    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    ResolvedAt  DATETIME2 NULL,
                    ResolvedBy  NVARCHAR(100) NULL,
                    IsDeleted   BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_DriverSosAlerts_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_DriverSosAlerts_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
                );
                CREATE INDEX IX_DriverSosAlerts_Driver ON DriverSosAlerts (TenantId, DriverId, CreatedAt DESC);
                CREATE INDEX IX_DriverSosAlerts_Open ON DriverSosAlerts (TenantId, Status, CreatedAt DESC) WHERE IsDeleted = 0;
            END

            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverAttendance')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverAttendance' AND COLUMN_NAME = 'AttendanceType')
                    ALTER TABLE DriverAttendance ADD AttendanceType NVARCHAR(20) NULL;

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverAttendance' AND COLUMN_NAME = 'RecordedAt')
                    ALTER TABLE DriverAttendance ADD RecordedAt DATETIME2 NULL;

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverAttendance' AND COLUMN_NAME = 'Latitude')
                    ALTER TABLE DriverAttendance ADD Latitude FLOAT NULL;

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverAttendance' AND COLUMN_NAME = 'Longitude')
                    ALTER TABLE DriverAttendance ADD Longitude FLOAT NULL;

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverAttendance' AND COLUMN_NAME = 'Notes')
                    ALTER TABLE DriverAttendance ADD Notes NVARCHAR(500) NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Driver app SOS / attendance columns migration completed.");
    }
}
