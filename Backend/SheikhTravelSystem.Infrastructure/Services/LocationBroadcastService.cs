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
        double? heading = null,
        decimal? fuelLevel = null,
        decimal? batteryLevel = null,
        int? gsmSignal = null,
        decimal? totalDistanceKm = null,
        string? address = null,
        string? alarmType = null,
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
            Timestamp = timestamp,
            Heading = heading,
            FuelLevel = fuelLevel,
            BatteryLevel = batteryLevel,
            GsmSignal = gsmSignal,
            TotalDistanceKm = totalDistanceKm,
            Address = address,
            AlarmType = alarmType
        };

        await hubContext.Clients.Group("dispatchers").SendAsync("ReceiveLocationUpdate", payload, cancellationToken);
        await hubContext.Clients.Group($"vehicle_{vehicleId}").SendAsync("ReceiveLocationUpdate", payload, cancellationToken);

        if (bookingId.HasValue)
        {
            await hubContext.Clients.Group($"booking_{bookingId.Value}")
                .SendAsync("ReceiveLocationUpdate", payload, cancellationToken);
        }
    }

    public async Task BroadcastSosAlertAsync(
        int vehicleId,
        double latitude,
        double longitude,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            VehicleId = vehicleId,
            Latitude = latitude,
            Longitude = longitude,
            Timestamp = timestamp
        };

        await hubContext.Clients.Group("dispatchers").SendAsync("ReceiveSosAlert", payload, cancellationToken);
        await hubContext.Clients.Group($"vehicle_{vehicleId}").SendAsync("ReceiveSosAlert", payload, cancellationToken);
    }
}
