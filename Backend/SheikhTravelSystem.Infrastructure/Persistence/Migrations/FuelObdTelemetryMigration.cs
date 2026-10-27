using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Optional OBD-II telemetry columns on GPS position storage (populated from Traccar/device attributes only).
/// </summary>
public static class FuelObdTelemetryMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('VehicleCurrentLocation', 'FuelConsumptionRateLph') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD FuelConsumptionRateLph DECIMAL(10,3) NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'ObdFuelUsedLiters') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD ObdFuelUsedLiters DECIMAL(14,3) NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'ObdOdometerKm') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD ObdOdometerKm DECIMAL(14,2) NULL;

                IF COL_LENGTH('GpsPositions', 'FuelConsumptionRateLph') IS NULL
                    ALTER TABLE GpsPositions ADD FuelConsumptionRateLph DECIMAL(10,3) NULL;
                IF COL_LENGTH('GpsPositions', 'ObdFuelUsedLiters') IS NULL
                    ALTER TABLE GpsPositions ADD ObdFuelUsedLiters DECIMAL(14,3) NULL;
                IF COL_LENGTH('GpsPositions', 'ObdOdometerKm') IS NULL
                    ALTER TABLE GpsPositions ADD ObdOdometerKm DECIMAL(14,2) NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("FuelObdTelemetryMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FuelObdTelemetryMigration failed.");
        }
    }
}
