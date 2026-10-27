using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Demo EV vehicles and charging POIs for superadmin tenant (1) when none exist.
/// Keeps /fleet/ev useful for walkthroughs without manual data entry.
/// </summary>
public static class ElectricFleetDemoSeedMigration
{
    private const int SuperAdminTenantId = 1;
    private const int ElectricFuelType = (int)FuelType.Electric;
    private const int AvailableStatus = (int)VehicleStatus.Available;

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var tenantExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Tenants WHERE Id = @Id",
            new { Id = SuperAdminTenantId },
            cancellationToken: cancellationToken));
        if (tenantExists == 0)
        {
            logger.LogInformation("ElectricFleetDemoSeedMigration skipped (tenant {TenantId} missing).", SuperAdminTenantId);
            return;
        }

        await SeedVehiclesAsync(connection, logger, cancellationToken);
        await SeedChargingStationsAsync(connection, logger, cancellationToken);
    }

    private static async Task SeedVehiclesAsync(
        System.Data.IDbConnection connection,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existingEv = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM Vehicles
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND FuelType = @Electric
            """,
            new { TenantId = SuperAdminTenantId, Electric = ElectricFuelType },
            cancellationToken: cancellationToken));

        if (existingEv > 0)
        {
            logger.LogInformation("ElectricFleetDemoSeedMigration skipped vehicles (tenant {TenantId} already has {Count} EVs).",
                SuperAdminTenantId, existingEv);
            return;
        }

        var specs = new[]
        {
            new
            {
                Name = "BYD e6 Shuttle",
                Reg = "EV-DEMO-001",
                Make = "BYD",
                Model = "e6",
                RangeKm = 400m,
                BatteryKwh = 82m,
                FuelLevel = 72m,
                Lat = 31.5204,
                Lon = 74.3587
            },
            new
            {
                Name = "MG ZS EV",
                Reg = "EV-DEMO-002",
                Make = "MG",
                Model = "ZS EV",
                RangeKm = 320m,
                BatteryKwh = 50m,
                FuelLevel = 14m,
                Lat = 31.5497,
                Lon = 74.3436
            }
        };

        foreach (var spec in specs)
        {
            var vehicleId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO Vehicles
                    (TenantId, Name, RegistrationNumber, Make, Model, [Year], SeatingCapacity, FuelAverage,
                     FuelType, ElectricRangeKm, BatteryCapacityKwh, CurrentMileage, InsuranceExpiryDate, Status, CreatedAt, CreatedBy, IsDeleted)
                OUTPUT INSERTED.Id
                VALUES
                    (@TenantId, @Name, @Reg, @Make, @Model, 2024, 5, 1,
                     @Electric, @RangeKm, @BatteryKwh, 12000, DATEADD(MONTH, 8, GETUTCDATE()), @Status, GETUTCDATE(), N'migration', 0)
                """,
                new
                {
                    TenantId = SuperAdminTenantId,
                    spec.Name,
                    Reg = spec.Reg,
                    spec.Make,
                    spec.Model,
                    Electric = ElectricFuelType,
                    spec.RangeKm,
                    spec.BatteryKwh,
                    Status = AvailableStatus
                },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                MERGE VehicleCurrentLocation AS target
                USING (SELECT @VehicleId AS VehicleId) AS source
                ON target.VehicleId = source.VehicleId
                WHEN MATCHED THEN
                    UPDATE SET FuelLevel = @FuelLevel, Latitude = @Lat, Longitude = @Lon, LastUpdate = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (VehicleId, Latitude, Longitude, FuelLevel, LastUpdate)
                    VALUES (@VehicleId, @Lat, @Lon, @FuelLevel, GETUTCDATE());
                """,
                new
                {
                    VehicleId = vehicleId,
                    spec.FuelLevel,
                    Lat = spec.Lat,
                    Lon = spec.Lon
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation("ElectricFleetDemoSeedMigration seeded {Count} demo EV vehicles for tenant {TenantId}.",
            specs.Length, SuperAdminTenantId);
    }

    private static async Task SeedChargingStationsAsync(
        System.Data.IDbConnection connection,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var tableExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ChargingStations'
            ) THEN 1 ELSE 0 END
            """,
            cancellationToken: cancellationToken));
        if (tableExists != 1)
            return;

        var existing = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM ChargingStations
            WHERE TenantId = @TenantId AND IsDeleted = 0
            """,
            new { TenantId = SuperAdminTenantId },
            cancellationToken: cancellationToken));

        if (existing > 0)
        {
            logger.LogInformation("ElectricFleetDemoSeedMigration skipped charging stations (tenant {TenantId} has {Count}).",
                SuperAdminTenantId, existing);
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO ChargingStations (TenantId, Name, Latitude, Longitude, Address, ConnectorType, IsActive)
            VALUES
                (@TenantId, N'Liberty EV Hub', 31.5102, 74.3440, N'MM Alam Road, Gulberg, Lahore', N'CCS2', 1),
                (@TenantId, N'DHA Phase 5 Fast Charge', 31.4675, 74.4112, N'DHA Phase 5, Lahore', N'CHAdeMO', 1);
            """,
            new { TenantId = SuperAdminTenantId },
            cancellationToken: cancellationToken));

        logger.LogInformation("ElectricFleetDemoSeedMigration seeded demo charging stations for tenant {TenantId}.", SuperAdminTenantId);
    }
}
