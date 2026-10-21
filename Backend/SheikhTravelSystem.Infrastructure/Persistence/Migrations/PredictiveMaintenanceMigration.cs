using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Vehicle-level fields for oil / tire / engine life predictive maintenance.
/// Defaults are applied in PredictiveMaintenanceHelper when columns are null.
/// </summary>
public static class PredictiveMaintenanceMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'OilIntervalKm')
                ALTER TABLE Vehicles ADD OilIntervalKm DECIMAL(12,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'OilIntervalDays')
                ALTER TABLE Vehicles ADD OilIntervalDays INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'LastOilChangeDate')
                ALTER TABLE Vehicles ADD LastOilChangeDate DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'LastOilChangeMileage')
                ALTER TABLE Vehicles ADD LastOilChangeMileage DECIMAL(12,2) NULL;

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'TireInstallDate')
                ALTER TABLE Vehicles ADD TireInstallDate DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'TireInstallMileage')
                ALTER TABLE Vehicles ADD TireInstallMileage DECIMAL(12,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'TireExpectedLifeKm')
                ALTER TABLE Vehicles ADD TireExpectedLifeKm DECIMAL(12,2) NULL;

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'EngineExpectedLifeKm')
                ALTER TABLE Vehicles ADD EngineExpectedLifeKm DECIMAL(12,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'EngineExpectedLifeYears')
                ALTER TABLE Vehicles ADD EngineExpectedLifeYears INT NULL;
            """, cancellationToken: cancellationToken));

        // Seed oil markers from active Oil Change schedules when still empty.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE v
            SET LastOilChangeDate = COALESCE(v.LastOilChangeDate, s.LastServiceDate),
                LastOilChangeMileage = COALESCE(v.LastOilChangeMileage, s.LastServiceMileage),
                OilIntervalKm = COALESCE(v.OilIntervalKm,
                    CASE WHEN s.IntervalType IN (N'Mileage') THEN CAST(s.IntervalValue AS DECIMAL(12,2)) ELSE NULL END),
                OilIntervalDays = COALESCE(v.OilIntervalDays,
                    CASE
                        WHEN s.IntervalType IN (N'Days', N'Day') THEN s.IntervalValue
                        WHEN s.IntervalType IN (N'Months', N'Month') THEN s.IntervalValue * 30
                        ELSE NULL
                    END)
            FROM Vehicles v
            INNER JOIN (
                SELECT VehicleId,
                    MAX(LastServiceDate) AS LastServiceDate,
                    MAX(LastServiceMileage) AS LastServiceMileage,
                    MAX(IntervalType) AS IntervalType,
                    MAX(IntervalValue) AS IntervalValue
                FROM VehicleMaintenanceSchedules
                WHERE IsDeleted = 0 AND IsActive = 1
                  AND (ServiceTypeName LIKE N'%oil%' OR ServiceTypeName LIKE N'%Oil%')
                GROUP BY VehicleId
            ) s ON s.VehicleId = v.Id
            WHERE v.IsDeleted = 0
              AND (v.LastOilChangeDate IS NULL OR v.LastOilChangeMileage IS NULL
                   OR v.OilIntervalKm IS NULL OR v.OilIntervalDays IS NULL);
            """, cancellationToken: cancellationToken));

        // Seed tire install from Tire Rotation / replacement schedules when empty.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE v
            SET TireInstallDate = COALESCE(v.TireInstallDate, s.LastServiceDate),
                TireInstallMileage = COALESCE(v.TireInstallMileage, s.LastServiceMileage),
                TireExpectedLifeKm = COALESCE(v.TireExpectedLifeKm,
                    CASE WHEN s.IntervalType = N'Mileage' AND s.IntervalValue >= 20000
                         THEN CAST(s.IntervalValue AS DECIMAL(12,2)) ELSE NULL END)
            FROM Vehicles v
            INNER JOIN (
                SELECT VehicleId,
                    MAX(LastServiceDate) AS LastServiceDate,
                    MAX(LastServiceMileage) AS LastServiceMileage,
                    MAX(IntervalType) AS IntervalType,
                    MAX(IntervalValue) AS IntervalValue
                FROM VehicleMaintenanceSchedules
                WHERE IsDeleted = 0 AND IsActive = 1
                  AND (ServiceTypeName LIKE N'%tire%' OR ServiceTypeName LIKE N'%Tire%'
                       OR ServiceTypeName LIKE N'%tyre%' OR ServiceTypeName LIKE N'%Tyre%')
                GROUP BY VehicleId
            ) s ON s.VehicleId = v.Id
            WHERE v.IsDeleted = 0
              AND (v.TireInstallDate IS NULL OR v.TireInstallMileage IS NULL OR v.TireExpectedLifeKm IS NULL);
            """, cancellationToken: cancellationToken));

        logger.LogInformation("PredictiveMaintenanceMigration applied (Vehicles oil/tire/engine life columns).");
    }
}
