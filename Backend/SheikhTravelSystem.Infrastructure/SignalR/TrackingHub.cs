using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SheikhTravelSystem.Infrastructure.SignalR;

[Authorize]
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

    public async Task JoinVehicleGroup(int vehicleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vehicle_{vehicleId}");
    }

    public async Task LeaveVehicleGroup(int vehicleId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"vehicle_{vehicleId}");
    }

    public async Task JoinBookingGroup(int bookingId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"booking_{bookingId}");
    }

    public async Task LeaveBookingGroup(int bookingId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"booking_{bookingId}");
    }
}
