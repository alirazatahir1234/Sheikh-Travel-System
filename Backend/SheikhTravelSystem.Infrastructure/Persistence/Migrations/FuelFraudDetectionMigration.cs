using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Driver pump orders + OBD verification / fuel fraud alerts.
/// </summary>
public static class FuelFraudDetectionMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FuelPumpOrders')
            BEGIN
                CREATE TABLE FuelPumpOrders (
                    Id                      INT IDENTITY(1,1) PRIMARY KEY,
                    TenantId                INT NOT NULL,
                    VehicleId               INT NOT NULL,
                    DriverId                INT NOT NULL,
                    OrderedLiters           DECIMAL(10,2) NOT NULL,
                    FuelLevelAtOrderPercent DECIMAL(6,2) NULL,
                    TankCapacityLiters      DECIMAL(10,2) NULL,
                    FuelLevelAfterPercent   DECIMAL(6,2) NULL,
                    ActualIncreaseLiters    DECIMAL(10,2) NULL,
                    ShortfallLiters         DECIMAL(10,2) NULL,
                    StationName             NVARCHAR(120) NULL,
                    Latitude                FLOAT NULL,
                    Longitude               FLOAT NULL,
                    Status                  NVARCHAR(30) NOT NULL DEFAULT N'PendingVerification',
                    GpsAlertEventId         INT NULL,
                    CreatedAtUtc            DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    VerifiedAtUtc           DATETIME2 NULL,
                    ResolvedAtUtc           DATETIME2 NULL,
                    ResolvedBy              NVARCHAR(100) NULL,
                    IsDeleted               BIT NOT NULL DEFAULT 0,
                    CONSTRAINT FK_FuelPumpOrders_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                    CONSTRAINT FK_FuelPumpOrders_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                    CONSTRAINT FK_FuelPumpOrders_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
                );
                CREATE INDEX IX_FuelPumpOrders_Tenant_Status ON FuelPumpOrders (TenantId, Status, CreatedAtUtc DESC)
                    WHERE IsDeleted = 0;
                CREATE INDEX IX_FuelPumpOrders_Vehicle_Pending ON FuelPumpOrders (VehicleId, Status, CreatedAtUtc DESC)
                    WHERE IsDeleted = 0 AND Status = N'PendingVerification';
            END
            """, cancellationToken: cancellationToken));

        if (await TableExistsAsync(connection, "Modules", cancellationToken))
        {
            const string code = "ALERT_FUEL_FRAUD";
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = @Code)
                    INSERT INTO Modules (ModuleCode, ModuleName, DisplayName, Description, Category, Icon, Route, SortOrder,
                        DependenciesJson, Visible, IsMobileSupported, IsAISupported, IsGPSSupported, Status, LegacyKeysJson)
                    VALUES (@Code, N'Fuel Fraud Detection', N'Fuel Fraud Detection',
                        N'Compare pump liters ordered vs OBD fuel level increase at petrol stations',
                        N'Fleet', N'gpp_bad', N'/fuel-logs/fraud-events', 215,
                        N'["ALERTS","GPS","fuel-logs"]', 1, 1, 0, 1, N'Active', N'["alert-fuel-fraud","fuel-fraud"]');
                """, new { Code = code }, cancellationToken: cancellationToken));

            if (await TableExistsAsync(connection, "TenantModules", cancellationToken))
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO TenantModules (TenantId, ModuleId)
                    SELECT DISTINCT tm.TenantId, m.Id
                    FROM TenantModules tm
                    INNER JOIN Modules parent ON parent.Id = tm.ModuleId AND parent.ModuleCode = N'ALERTS'
                    CROSS JOIN Modules m
                    WHERE m.ModuleCode = @Code
                      AND NOT EXISTS (
                          SELECT 1 FROM TenantModules x
                          WHERE x.TenantId = tm.TenantId AND x.ModuleId = m.Id)
                    """, new { Code = code }, cancellationToken: cancellationToken));
            }
        }

        logger.LogInformation("FuelFraudDetectionMigration applied successfully.");
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.IDbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(@Table, 'U') IS NOT NULL THEN 1 ELSE 0 END",
            new { Table = table },
            cancellationToken: cancellationToken)) == 1;
    }
}
