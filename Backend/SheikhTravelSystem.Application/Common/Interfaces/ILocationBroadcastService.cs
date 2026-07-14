namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ILocationBroadcastService
{
    Task BroadcastLocationUpdateAsync(
        int vehicleId,
        int? bookingId,
        double latitude,
        double longitude,
        decimal speed,
        bool? ignition,
        DateTime timestamp,
        double? heading = null,
        decimal? fuelLevel = null,
        decimal? batteryLevel = null,
        int? gsmSignal = null,
        decimal? totalDistanceKm = null,
        string? address = null,
        string? alarmType = null,
        decimal? temperature = null,
        CancellationToken cancellationToken = default);

    Task BroadcastSosAlertAsync(
        int vehicleId,
        double latitude,
        double longitude,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}
