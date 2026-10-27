using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// SOS auto-dispatch: enriched driver SOS rows + regional recovery partners.
/// </summary>
public static class SosAutoDispatchMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverSosAlerts')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'SpeedKmh')
                    ALTER TABLE DriverSosAlerts ADD SpeedKmh DECIMAL(8,2) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'LastRouteJson')
                    ALTER TABLE DriverSosAlerts ADD LastRouteJson NVARCHAR(MAX) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'LastRouteSummary')
                    ALTER TABLE DriverSosAlerts ADD LastRouteSummary NVARCHAR(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'RegionCode')
                    ALTER TABLE DriverSosAlerts ADD RegionCode NVARCHAR(10) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'AssignedResponderDriverId')
                    ALTER TABLE DriverSosAlerts ADD AssignedResponderDriverId INT NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'DispatchMode')
                    ALTER TABLE DriverSosAlerts ADD DispatchMode NVARCHAR(20) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'DistanceKm')
                    ALTER TABLE DriverSosAlerts ADD DistanceKm DECIMAL(10,2) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'EtaMinutes')
                    ALTER TABLE DriverSosAlerts ADD EtaMinutes INT NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'ConfirmationMessage')
                    ALTER TABLE DriverSosAlerts ADD ConfirmationMessage NVARCHAR(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DriverSosAlerts' AND COLUMN_NAME = 'SuggestedPartnerId')
                    ALTER TABLE DriverSosAlerts ADD SuggestedPartnerId INT NULL;
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SosRecoveryPartners')
            BEGIN
                CREATE TABLE SosRecoveryPartners (
                    Id          INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId    INT NOT NULL,
                    RegionCode  NVARCHAR(10) NOT NULL,
                    PartnerType NVARCHAR(30) NOT NULL,
                    Name        NVARCHAR(200) NOT NULL,
                    Phone       NVARCHAR(40) NOT NULL,
                    Email       NVARCHAR(200) NULL,
                    Notes       NVARCHAR(500) NULL,
                    IsActive    BIT NOT NULL DEFAULT 1,
                    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt   DATETIME2 NULL,
                    IsDeleted   BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_SosRecoveryPartners_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
                );
                CREATE INDEX IX_SosRecoveryPartners_TenantRegion
                    ON SosRecoveryPartners (TenantId, RegionCode, PartnerType) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO SosRecoveryPartners (TenantId, RegionCode, PartnerType, Name, Phone, Email, Notes)
            SELECT t.Id, v.RegionCode, v.PartnerType, v.Name, v.Phone, v.Email, v.Notes
            FROM Tenants t
            CROSS APPLY (VALUES
                (N'PK', N'Towing',   N'SheikhGo Recovery Lahore',  N'+92-42-111-SOS-01', N'recovery.pk@sheikhgo.com', N'24/7 towing — Punjab'),
                (N'PK', N'FlatTire', N'Quick Tyre PK',             N'+92-300-555-0199',  NULL, N'Mobile flat repair'),
                (N'PK', N'Medical',  N'Rescue 1122 Partner',       N'1122',              NULL, N'Emergency medical coordination'),
                (N'AE', N'Towing',   N'Dubai Fleet Assist',        N'+971-4-555-0100',  N'assist@sheikhgo.ae', N'Dubai & Northern Emirates'),
                (N'AE', N'FlatTire', N'Roadside UAE Tyres',       N'+971-50-555-0202', NULL, N'Mobile tyre service'),
                (N'AE', N'Medical',  N'Dubai Ambulance Dispatch',  N'998',               NULL, N'Medical emergency line')
            ) v(RegionCode, PartnerType, Name, Phone, Email, Notes)
            WHERE NOT EXISTS (
                SELECT 1 FROM SosRecoveryPartners p
                WHERE p.TenantId = t.Id AND p.RegionCode = v.RegionCode
                  AND p.PartnerType = v.PartnerType AND p.IsDeleted = 0
            );
            """, cancellationToken: cancellationToken));

        logger.LogInformation("SOS auto-dispatch migration completed.");
    }
}
