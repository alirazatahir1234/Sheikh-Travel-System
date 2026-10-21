using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.SignalR;

/// <summary>
/// Live GPS / SOS hub. Groups are tenant-scoped (same pattern as <see cref="NotificationHub"/>)
/// so dispatchers never receive another tenant's positions.
/// </summary>
[Authorize]
public class TrackingHub(IDbConnectionFactory dbFactory) : Hub
{
    public static string DispatcherGroup(int tenantId) => $"tenant_{tenantId}:dispatchers";
    public static string VehicleGroup(int tenantId, int vehicleId) => $"tenant_{tenantId}:vehicle_{vehicleId}";
    public static string BookingGroup(int tenantId, int bookingId) => $"tenant_{tenantId}:booking_{bookingId}";

    public override Task OnConnectedAsync()
    {
        TrackingHubMetrics.Increment();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        TrackingHubMetrics.Decrement();
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client-pushed fake positions are not allowed. Authoritative fixes come from
    /// <see cref="Services.LocationBroadcastService"/> after server-side ingest.
    /// </summary>
    public Task SendLocationUpdate(int vehicleId, double latitude, double longitude, decimal speed)
        => throw new HubException("Client location broadcast is not allowed.");

    public async Task JoinDispatcherGroup()
    {
        if (!TryGetTenantId(out var tenantId))
            throw new HubException("Tenant required to join dispatcher group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, DispatcherGroup(tenantId));
    }

    public async Task LeaveDispatcherGroup()
    {
        if (!TryGetTenantId(out var tenantId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, DispatcherGroup(tenantId));
    }

    public async Task JoinVehicleGroup(int vehicleId)
    {
        if (!TryGetTenantId(out var tenantId))
            throw new HubException("Tenant required to join vehicle group.");

        if (!await VehicleBelongsToTenantAsync(vehicleId, tenantId))
            throw new HubException("Not authorized to join this vehicle group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, VehicleGroup(tenantId, vehicleId));
    }

    public async Task LeaveVehicleGroup(int vehicleId)
    {
        if (!TryGetTenantId(out var tenantId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, VehicleGroup(tenantId, vehicleId));
    }

    public async Task JoinBookingGroup(int bookingId)
    {
        if (!TryGetTenantId(out var tenantId))
            throw new HubException("Tenant required to join booking group.");

        if (!await BookingBelongsToTenantAsync(bookingId, tenantId))
            throw new HubException("Not authorized to join this booking group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, BookingGroup(tenantId, bookingId));
    }

    public async Task LeaveBookingGroup(int bookingId)
    {
        if (!TryGetTenantId(out var tenantId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BookingGroup(tenantId, bookingId));
    }

    private bool TryGetTenantId(out int tenantId)
    {
        tenantId = 0;
        var claim = Context.User?.FindFirst("tenantId")?.Value
                    ?? Context.User?.FindFirst("tenant_id")?.Value;
        return int.TryParse(claim, out tenantId) && tenantId > 0;
    }

    private async Task<bool> VehicleBelongsToTenantAsync(int vehicleId, int tenantId)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Vehicles
                WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
            ) THEN 1 ELSE 0 END
            """,
            new { VehicleId = vehicleId, TenantId = tenantId }));
    }

    private async Task<bool> BookingBelongsToTenantAsync(int bookingId, int tenantId)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Bookings
                WHERE Id = @BookingId AND TenantId = @TenantId AND IsDeleted = 0
            ) THEN 1 ELSE 0 END
            """,
            new { BookingId = bookingId, TenantId = tenantId }));
    }
}
