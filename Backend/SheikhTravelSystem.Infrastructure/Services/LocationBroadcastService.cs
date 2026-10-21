using Dapper;
using Microsoft.AspNetCore.SignalR;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.SignalR;

namespace SheikhTravelSystem.Infrastructure.Services;

public class LocationBroadcastService(
    IHubContext<TrackingHub> hubContext,
    IDbConnectionFactory dbFactory) : ILocationBroadcastService
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
        decimal? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveTenantIdAsync(vehicleId, cancellationToken);
        if (tenantId is null or <= 0)
            return;

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
            AlarmType = alarmType,
            Temperature = temperature
        };

        await hubContext.Clients.Group(TrackingHub.DispatcherGroup(tenantId.Value))
            .SendAsync("ReceiveLocationUpdate", payload, cancellationToken);
        await hubContext.Clients.Group(TrackingHub.VehicleGroup(tenantId.Value, vehicleId))
            .SendAsync("ReceiveLocationUpdate", payload, cancellationToken);

        if (bookingId.HasValue)
        {
            await hubContext.Clients.Group(TrackingHub.BookingGroup(tenantId.Value, bookingId.Value))
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
        var tenantId = await ResolveTenantIdAsync(vehicleId, cancellationToken);
        if (tenantId is null or <= 0)
            return;

        var payload = new
        {
            VehicleId = vehicleId,
            Latitude = latitude,
            Longitude = longitude,
            Timestamp = timestamp
        };

        await hubContext.Clients.Group(TrackingHub.DispatcherGroup(tenantId.Value))
            .SendAsync("ReceiveSosAlert", payload, cancellationToken);
        await hubContext.Clients.Group(TrackingHub.VehicleGroup(tenantId.Value, vehicleId))
            .SendAsync("ReceiveSosAlert", payload, cancellationToken);
    }

    private async Task<int?> ResolveTenantIdAsync(int vehicleId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @VehicleId AND IsDeleted = 0",
            new { VehicleId = vehicleId },
            cancellationToken: cancellationToken));
    }
}
