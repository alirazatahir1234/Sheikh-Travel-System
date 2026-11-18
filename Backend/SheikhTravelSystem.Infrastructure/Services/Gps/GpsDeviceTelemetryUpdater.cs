using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsDeviceTelemetryUpdater : IGpsDeviceTelemetryUpdater
{
    public Task UpdateAsync(
        IDbConnection connection,
        int gpsDeviceId,
        DateTime timestamp,
        bool? ignition,
        decimal? speed = null,
        decimal? batteryLevel = null,
        int? rssi = null,
        bool clearStickyIgnition = false,
        int? expectedVehicleId = null,
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsDevices SET
                LastSeenAt = @Timestamp,
                LastIgnition = CASE
                    WHEN @ClearStickyIgnition = 1 THEN NULL
                    ELSE COALESCE(@Ignition, LastIgnition)
                  END,
                LastSpeed = COALESCE(@Speed, LastSpeed),
                LastBatteryLevel = COALESCE(@BatteryLevel, LastBatteryLevel),
                LastRssi = COALESCE(@Rssi, LastRssi),
                UpdatedAt = @Timestamp
              WHERE Id = @Id AND IsDeleted = 0
                AND (@ExpectedVehicleId IS NULL OR VehicleId = @ExpectedVehicleId)",
            new
            {
                Id = gpsDeviceId,
                Timestamp = timestamp,
                Ignition = ignition,
                Speed = speed,
                BatteryLevel = batteryLevel,
                Rssi = rssi,
                ClearStickyIgnition = clearStickyIgnition,
                // SG-01 Step 6a: ingest passes the validated vehicle so a device id cannot touch another
                // vehicle's device. Traccar provisioning of unassigned devices passes null (unchanged).
                ExpectedVehicleId = expectedVehicleId
            },
            cancellationToken: cancellationToken));
    }
}
