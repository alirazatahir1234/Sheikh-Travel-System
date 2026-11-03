using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Formerly seeded demo EV vehicles (EV-DEMO-001 / EV-DEMO-002) and charging POIs for tenant 1.
/// Vehicle seeding is permanently disabled — production fleets must not get dummy vehicles on migrate.
/// Charging-station seed remains idempotent for empty tenants only.
/// </summary>
public static class ElectricFleetDemoSeedMigration
{
    private const int SuperAdminTenantId = 1;

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

        // Demo vehicle INSERT removed: EV-DEMO-001 / EV-DEMO-002 must never be auto-created.
        logger.LogInformation(
            "ElectricFleetDemoSeedMigration: demo EV vehicle seeding disabled (no EV-DEMO inserts).");

        await SeedChargingStationsAsync(connection, logger, cancellationToken);
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
