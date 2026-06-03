using Microsoft.AspNetCore.SignalR;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.SignalR;

namespace SheikhTravelSystem.Infrastructure.Services;

public class LocationBroadcastService(IHubContext<TrackingHub> hubContext) : ILocationBroadcastService
{
    public async Task BroadcastLocationUpdateAsync(
        int vehicleId,
        double latitude,
        double longitude,
        decimal speed,
        bool? ignition,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.Group("dispatchers").SendAsync(
            "ReceiveLocationUpdate",
            new
            {
                VehicleId = vehicleId,
                Latitude = latitude,
                Longitude = longitude,
                Speed = speed,
                Ignition = ignition,
                Timestamp = timestamp
            },
            cancellationToken);
    }
}
