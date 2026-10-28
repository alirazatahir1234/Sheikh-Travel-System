using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Module 1 — tenant score weights + score history for explainable driver performance.
/// </summary>
public static class DriverPerformanceScoringMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DriverPerformanceScoreWeights')
            BEGIN
                CREATE TABLE DriverPerformanceScoreWeights (
                    TenantId        INT NOT NULL PRIMARY KEY,
                    SafetyWeight    DECIMAL(9,2) NOT NULL DEFAULT 40,
                    FuelWeight      DECIMAL(9,2) NOT NULL DEFAULT 30,
                    OnTimeWeight    DECIMAL(9,2) NOT NULL DEFAULT 30,
                    UpdatedBy       NVARCHAR(100) NULL,
                    UpdatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_DriverPerfWeights_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
                );
            END

            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DriverPerformanceScoreHistory')
            BEGIN
                CREATE TABLE DriverPerformanceScoreHistory (
                    Id                      INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId                INT NOT NULL,
                    DriverId                INT NOT NULL,
                    PeriodFrom              DATETIME2 NOT NULL,
                    PeriodTo                DATETIME2 NOT NULL,
                    OverallScore            INT NOT NULL,
                    SafetyScore             INT NOT NULL,
                    FuelScore               INT NOT NULL,
                    OnTimeScore             INT NOT NULL,
                    SecondaryBehaviorScore  INT NOT NULL DEFAULT 0,
                    PreviousOverallScore    INT NULL,
                    Delta                   INT NOT NULL DEFAULT 0,
                    Explanation             NVARCHAR(1000) NOT NULL,
                    WeightsJson             NVARCHAR(200) NULL,
                    FactorsJson             NVARCHAR(MAX) NULL,
                    PrimaryVehicleCategory  NVARCHAR(60) NULL,
                    KmPerLiter              DECIMAL(12,3) NULL,
                    PeerMedianKmPerLiter    DECIMAL(12,3) NULL,
                    FuelUsedPeerBand        BIT NOT NULL DEFAULT 0,
                    IsPartial               BIT NOT NULL DEFAULT 0,
                    ComputedAt              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_DriverPerfHistory_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_DriverPerfHistory_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
                );
                CREATE INDEX IX_DriverPerfHistory_Driver_Period
                    ON DriverPerformanceScoreHistory (TenantId, DriverId, PeriodFrom, PeriodTo, ComputedAt DESC);
                CREATE INDEX IX_DriverPerfHistory_Tenant_Computed
                    ON DriverPerformanceScoreHistory (TenantId, ComputedAt DESC);
            END

            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
            AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
            INSERT INTO Permissions (ModuleName, PermissionCode, Description)
            VALUES (N'Fleet', @Code, N'Manage driver performance scoring weights');
            """,
            new { Code = DriverPermissions.DriverManagePerformanceWeights },
            cancellationToken: cancellationToken));

        string[] roles = ["TENANT_ADMIN", "OWNER", "FLEET_MANAGER", "DRIVER_MANAGER"];
        foreach (var role in roles)
        {
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection,
                role,
                [DriverPermissions.DriverManagePerformanceWeights],
                cancellationToken);
        }

        logger.LogInformation("Driver performance scoring (weights/history) migration completed.");
    }
}
