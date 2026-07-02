using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsTelemetryFieldsMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('VehicleCurrentLocation', 'FuelLevel') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD FuelLevel DECIMAL(5,2) NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'BatteryLevel') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD BatteryLevel DECIMAL(5,2) NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'GsmSignal') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD GsmSignal INT NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'TotalDistanceKm') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD TotalDistanceKm DECIMAL(12,2) NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'Address') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD Address NVARCHAR(500) NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'AlarmType') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD AlarmType NVARCHAR(50) NULL;
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsPositions', 'FuelLevel') IS NULL
                    ALTER TABLE GpsPositions ADD FuelLevel DECIMAL(5,2) NULL;
                IF COL_LENGTH('GpsPositions', 'BatteryLevel') IS NULL
                    ALTER TABLE GpsPositions ADD BatteryLevel DECIMAL(5,2) NULL;
                IF COL_LENGTH('GpsPositions', 'GsmSignal') IS NULL
                    ALTER TABLE GpsPositions ADD GsmSignal INT NULL;
                IF COL_LENGTH('GpsPositions', 'TotalDistanceKm') IS NULL
                    ALTER TABLE GpsPositions ADD TotalDistanceKm DECIMAL(12,2) NULL;
                IF COL_LENGTH('GpsPositions', 'Address') IS NULL
                    ALTER TABLE GpsPositions ADD Address NVARCHAR(500) NULL;
                IF COL_LENGTH('GpsPositions', 'AlarmType') IS NULL
                    ALTER TABLE GpsPositions ADD AlarmType NVARCHAR(50) NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsTelemetryFieldsMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsTelemetryFieldsMigration failed.");
        }
    }
}
