using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class DriverDeviceRegistrationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverDevices')
            BEGIN
                CREATE TABLE DriverDevices (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    DriverId INT NOT NULL,
                    UserId INT NULL,
                    DeviceId NVARCHAR(200) NOT NULL,
                    Platform NVARCHAR(40) NOT NULL,
                    Model NVARCHAR(200) NULL,
                    OsVersion NVARCHAR(100) NULL,
                    AppVersion NVARCHAR(40) NULL,
                    PackageName NVARCHAR(200) NULL,
                    InstallerStore NVARCHAR(200) NULL,
                    FingerprintHash NVARCHAR(128) NULL,
                    IsEmulator BIT NOT NULL DEFAULT 0,
                    IsRooted BIT NOT NULL DEFAULT 0,
                    IsJailbroken BIT NOT NULL DEFAULT 0,
                    IsTampered BIT NOT NULL DEFAULT 0,
                    PinningConfigured BIT NOT NULL DEFAULT 0,
                    LastSeenAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CONSTRAINT UQ_DriverDevices_Driver_Device UNIQUE (DriverId, DeviceId)
                );
                CREATE INDEX IX_DriverDevices_Tenant_Driver ON DriverDevices (TenantId, DriverId)
                    WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("DriverDeviceRegistrationMigration applied.");
    }
}
