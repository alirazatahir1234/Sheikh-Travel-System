using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// EV fleet fields, charging POIs, driving anomaly events, and vehicle document expiry columns.
/// </summary>
public static class FleetEvAnomalyMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('Vehicles', 'RegistrationExpiryDate') IS NULL
                ALTER TABLE Vehicles ADD RegistrationExpiryDate DATETIME2 NULL;
            IF COL_LENGTH('Vehicles', 'FitnessCertificateExpiryDate') IS NULL
                ALTER TABLE Vehicles ADD FitnessCertificateExpiryDate DATETIME2 NULL;
            IF COL_LENGTH('Vehicles', 'ElectricRangeKm') IS NULL
                ALTER TABLE Vehicles ADD ElectricRangeKm DECIMAL(10,2) NULL;
            IF COL_LENGTH('Vehicles', 'BatteryCapacityKwh') IS NULL
                ALTER TABLE Vehicles ADD BatteryCapacityKwh DECIMAL(10,2) NULL;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ChargingStations')
            CREATE TABLE ChargingStations (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                Name NVARCHAR(150) NOT NULL,
                Latitude FLOAT NOT NULL,
                Longitude FLOAT NOT NULL,
                Address NVARCHAR(300) NULL,
                ConnectorType NVARCHAR(60) NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_ChargingStations_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChargingStations_Tenant')
                CREATE INDEX IX_ChargingStations_Tenant ON ChargingStations (TenantId) WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FleetAnomalyEvents')
            CREATE TABLE FleetAnomalyEvents (
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                VehicleId INT NOT NULL,
                DriverId INT NULL,
                AnomalyType NVARCHAR(40) NOT NULL,
                Severity NVARCHAR(20) NOT NULL DEFAULT N'medium',
                Latitude FLOAT NULL,
                Longitude FLOAT NULL,
                SpeedKmh DECIMAL(8,2) NULL,
                Details NVARCHAR(500) NULL,
                IsReviewed BIT NOT NULL DEFAULT 0,
                ReviewedAt DATETIME2 NULL,
                ReviewedBy NVARCHAR(100) NULL,
                ReviewNotes NVARCHAR(500) NULL,
                CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_FleetAnomalyEvents_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetAnomalyEvents_Tenant_Created')
                CREATE INDEX IX_FleetAnomalyEvents_Tenant_Created
                    ON FleetAnomalyEvents (TenantId, CreatedAtUtc DESC) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetAnomalyEvents_Vehicle')
                CREATE INDEX IX_FleetAnomalyEvents_Vehicle
                    ON FleetAnomalyEvents (VehicleId, CreatedAtUtc DESC) WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        logger.LogInformation("FleetEvAnomalyMigration completed.");
    }
}
