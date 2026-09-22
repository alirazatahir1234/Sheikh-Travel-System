using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsDevicesTenantMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            // Add TenantId column (nullable so Traccar-synced devices start unowned)
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsDevices', 'TenantId') IS NULL
                    ALTER TABLE GpsDevices ADD TenantId INT NULL;
                """, cancellationToken: cancellationToken));

            // Backfill only when Vehicles.TenantId exists (TenantSchemaMigration must run first).
            if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT CASE WHEN COL_LENGTH('Vehicles', 'TenantId') IS NULL THEN 0 ELSE 1 END",
                    cancellationToken: cancellationToken)) == 1)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE gd
                    SET gd.TenantId = v.TenantId
                    FROM GpsDevices gd
                    INNER JOIN Vehicles v ON v.Id = gd.VehicleId
                    WHERE gd.TenantId IS NULL AND gd.VehicleId IS NOT NULL AND gd.IsDeleted = 0;
                    """, cancellationToken: cancellationToken));
            }
            else
            {
                logger.LogWarning(
                    "GpsDevicesTenantMigration: Vehicles.TenantId missing — skipped backfill. Ensure TenantSchemaMigration runs first.");
            }

            logger.LogInformation("GpsDevicesTenantMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDevicesTenantMigration failed.");
            throw;
        }
    }
}
