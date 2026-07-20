using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.SignalR;

[Authorize]
public class NotificationHub(IUserPresenceService presence) : Hub
{
    public static string UserGroup(int tenantId, int userId) => $"tenant_{tenantId}:user_{userId}";
    public static string DispatcherGroup(int tenantId) => $"tenant_{tenantId}:dispatchers";

    public override async Task OnConnectedAsync()
    {
        var tenantId = 0;
        if (TryGetIdentity(out tenantId, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(tenantId, userId));
            await presence.SetBrowserOnlineAsync(userId, true);
        }

        if (tenantId > 0 && IsDispatcherOrAdmin())
            await Groups.AddToGroupAsync(Context.ConnectionId, DispatcherGroup(tenantId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetIdentity(out _, out var userId))
            await presence.SetBrowserOnlineAsync(userId, false);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinUserGroup(int userId)
    {
        if (!TryGetIdentity(out var tenantId, out var callerUserId) || userId != callerUserId)
            throw new HubException("Not authorized to join this user group.");

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(tenantId, callerUserId));
    }

    public async Task Heartbeat()
    {
        if (TryGetIdentity(out _, out var userId))
            await presence.SetBrowserOnlineAsync(userId, true);
    }

    private bool TryGetIdentity(out int tenantId, out int userId)
    {
        tenantId = 0;
        userId = 0;
        var tenantClaim = Context.User?.FindFirst("tenantId")?.Value
                          ?? Context.User?.FindFirst("tenant_id")?.Value;
        var userClaim = Context.User?.FindFirst("userId")?.Value;
        return int.TryParse(tenantClaim, out tenantId) && int.TryParse(userClaim, out userId);
    }

    private bool IsDispatcherOrAdmin()
    {
        var roles = Context.User?.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        return roles.Contains("Admin") || roles.Contains("Dispatcher") || roles.Contains("SuperAdmin");
    }
}

public sealed class NotificationRealtimePublisher(IHubContext<NotificationHub> hub) : INotificationRealtimePublisher
{
    public Task PublishToUserAsync(int userId, object payload, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(NotificationHub.UserGroup(GetTenantId(payload), userId))
            .SendAsync("ReceiveNotification", payload, cancellationToken);

    public Task PublishToDispatchersAsync(object payload, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(NotificationHub.DispatcherGroup(GetTenantId(payload)))
            .SendAsync("ReceiveNotification", payload, cancellationToken);

    private static int GetTenantId(object payload)
    {
        var prop = payload.GetType().GetProperty("tenantId");
        if (prop?.GetValue(payload) is int tid && tid > 0)
            return tid;
        return 1;
    }
}
