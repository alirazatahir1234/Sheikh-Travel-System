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
        CancellationToken cancellationToken = default);
}
