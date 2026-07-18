using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.SignalR;

[Authorize]
public class NotificationHub(IUserPresenceService presence) : Hub
{
    public static string UserGroup(int userId) => $"user_{userId}";

    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.User?.FindFirst("userId")?.Value, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            await presence.SetBrowserOnlineAsync(userId, true);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "notification_dispatchers");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (int.TryParse(Context.User?.FindFirst("userId")?.Value, out var userId))
            await presence.SetBrowserOnlineAsync(userId, false);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinUserGroup(int userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
    }

    public async Task Heartbeat()
    {
        if (int.TryParse(Context.User?.FindFirst("userId")?.Value, out var userId))
            await presence.SetBrowserOnlineAsync(userId, true);
    }
}

public sealed class NotificationRealtimePublisher(IHubContext<NotificationHub> hub) : INotificationRealtimePublisher
{
    public Task PublishToUserAsync(int userId, object payload, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(NotificationHub.UserGroup(userId))
            .SendAsync("ReceiveNotification", payload, cancellationToken);

    public Task PublishToDispatchersAsync(object payload, CancellationToken cancellationToken = default) =>
        hub.Clients.Group("notification_dispatchers")
            .SendAsync("ReceiveNotification", payload, cancellationToken);
}
