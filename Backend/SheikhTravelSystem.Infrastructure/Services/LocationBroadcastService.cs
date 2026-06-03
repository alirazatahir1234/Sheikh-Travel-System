using Microsoft.AspNetCore.SignalR;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.SignalR;

namespace SheikhTravelSystem.Infrastructure.Services;

public class LocationBroadcastService(IHubContext<TrackingHub> hubContext) : ILocationBroadcastService
{
    public async Task BroadcastLocationUpdateAsync(
        int vehicleId,
        int? bookingId,
        double latitude,
        double longitude,
        decimal speed,
        bool? ignition,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            VehicleId = vehicleId,
            BookingId = bookingId,
            Latitude = latitude,
            Longitude = longitude,
            Speed = speed,
            Ignition = ignition,
            Timestamp = timestamp
        };

        await hubContext.Clients.Group("dispatchers").SendAsync("ReceiveLocationUpdate", payload, cancellationToken);
        await hubContext.Clients.Group($"vehicle_{vehicleId}").SendAsync("ReceiveLocationUpdate", payload, cancellationToken);

        if (bookingId.HasValue)
        {
            await hubContext.Clients.Group($"booking_{bookingId.Value}")
                .SendAsync("ReceiveLocationUpdate", payload, cancellationToken);
        }
    }
}
