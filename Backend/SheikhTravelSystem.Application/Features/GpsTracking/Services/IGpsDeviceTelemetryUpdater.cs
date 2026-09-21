using System.Data;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsDeviceTelemetryUpdater
{
    Task UpdateAsync(
        IDbConnection connection,
        int gpsDeviceId,
        DateTime timestamp,
        bool? ignition,
        decimal? speed = null,
        decimal? batteryLevel = null,
        int? rssi = null,
        CancellationToken cancellationToken = default);
}
