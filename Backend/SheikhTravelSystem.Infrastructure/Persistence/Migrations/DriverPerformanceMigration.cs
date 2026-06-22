using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class DriverPerformanceMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DriverViolations')
            BEGIN
                CREATE TABLE DriverViolations (
                    Id              INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId        INT NOT NULL,
                    DriverId        INT NOT NULL,
                    ViolationType   NVARCHAR(50) NOT NULL,
                    Severity        NVARCHAR(20) NOT NULL,
                    OccurredAt      DATETIME2 NOT NULL,
                    Description     NVARCHAR(500) NULL,
                    BookingId       INT NULL,
                    GpsAlertId      INT NULL,
                    Status          NVARCHAR(20) NOT NULL DEFAULT N'Open',
                    CreatedBy       NVARCHAR(100) NULL,
                    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    IsDeleted       BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_DriverViolations_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_DriverViolations_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
                );
                CREATE INDEX IX_DriverViolations_Driver ON DriverViolations (TenantId, DriverId, OccurredAt DESC);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DriverAttendance')
            BEGIN
                CREATE TABLE DriverAttendance (
                    Id              INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId        INT NOT NULL,
                    DriverId        INT NOT NULL,
                    AttendanceDate  DATE NOT NULL,
                    Status          NVARCHAR(20) NOT NULL,
                    CheckInAt       DATETIME2 NULL,
                    CheckOutAt      DATETIME2 NULL,
                    Notes           NVARCHAR(500) NULL,
                    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    IsDeleted       BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_DriverAttendance_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_DriverAttendance_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
                );
                CREATE UNIQUE INDEX UX_DriverAttendance_Date ON DriverAttendance (TenantId, DriverId, AttendanceDate) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Driver performance schema migration completed.");
    }
}
