using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Driver mobile idempotency ledger + voice command audit rows. Idempotent — safe to re-run.
/// </summary>
public static class DriverMobileSyncMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverMobileIdempotency')
            BEGIN
                CREATE TABLE DriverMobileIdempotency (
                    Id            INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId      INT NOT NULL,
                    DriverId      INT NOT NULL,
                    ClientOpId    NVARCHAR(64) NOT NULL,
                    OpType        NVARCHAR(64) NOT NULL,
                    ResponseJson  NVARCHAR(MAX) NOT NULL,
                    HttpStatus    INT NOT NULL,
                    CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_DriverMobileIdempotency_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_DriverMobileIdempotency_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id),
                    CONSTRAINT UQ_DriverMobileIdempotency_ClientOp UNIQUE (TenantId, ClientOpId)
                );
                CREATE INDEX IX_DriverMobileIdempotency_Driver ON DriverMobileIdempotency (TenantId, DriverId, CreatedAt DESC);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverVoiceCommands')
            BEGIN
                CREATE TABLE DriverVoiceCommands (
                    Id            INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId      INT NOT NULL,
                    DriverId      INT NOT NULL,
                    ClientOpId    NVARCHAR(64) NULL,
                    LanguageCode  NVARCHAR(8) NOT NULL,
                    Transcript    NVARCHAR(2000) NOT NULL,
                    Intent        NVARCHAR(32) NOT NULL,
                    VehicleId     INT NULL,
                    BookingId     INT NULL,
                    Latitude      FLOAT NULL,
                    Longitude     FLOAT NULL,
                    SosAlertId    INT NULL,
                    CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    IsDeleted     BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_DriverVoiceCommands_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_DriverVoiceCommands_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
                );
                CREATE INDEX IX_DriverVoiceCommands_Driver ON DriverVoiceCommands (TenantId, DriverId, CreatedAt DESC);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Driver mobile sync / voice migration completed.");
    }
}
