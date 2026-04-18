using Microsoft.AspNetCore.SignalR;

namespace SheikhTravelSystem.Infrastructure.SignalR;

public class TrackingHub : Hub
{
    public async Task SendLocationUpdate(int vehicleId, double latitude, double longitude, decimal speed)
    {
        await Clients.Group("dispatchers").SendAsync("ReceiveLocationUpdate", new
        {
            VehicleId = vehicleId,
            Latitude = latitude,
            Longitude = longitude,
            Speed = speed,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task JoinDispatcherGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dispatchers");
    }

    public async Task LeaveDispatcherGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dispatchers");
    }
}
