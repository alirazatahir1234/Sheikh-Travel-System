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
        CancellationToken cancellationToken = default)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsDevices SET
                LastSeenAt = @Timestamp,
                LastIgnition = @Ignition,
                LastSpeed = COALESCE(@Speed, LastSpeed),
                LastBatteryLevel = COALESCE(@BatteryLevel, LastBatteryLevel),
                LastRssi = COALESCE(@Rssi, LastRssi),
                UpdatedAt = @Timestamp
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                Id = gpsDeviceId,
                Timestamp = timestamp,
                Ignition = ignition,
                Speed = speed,
                BatteryLevel = batteryLevel,
                Rssi = rssi
            },
            cancellationToken: cancellationToken));
    }
}
