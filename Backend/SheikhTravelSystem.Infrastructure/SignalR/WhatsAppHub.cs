using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.SignalR;

[Authorize]
public class WhatsAppHub : Hub
{
    public static string TenantGroup(int tenantId) => $"whatsapp:tenant_{tenantId}";

    public override async Task OnConnectedAsync()
    {
        if (TryGetTenantId(out var tenantId))
            await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
        await base.OnConnectedAsync();
    }

    private bool TryGetTenantId(out int tenantId)
    {
        tenantId = 0;
        var claim = Context.User?.FindFirst("tenantId")?.Value
                    ?? Context.User?.FindFirst("tenant_id")?.Value;
        return int.TryParse(claim, out tenantId) && tenantId > 0;
    }
}

public sealed class WhatsAppRealtimePublisher(IHubContext<WhatsAppHub> hub) : IWhatsAppRealtimePublisher
{
    public Task PublishAsync(int tenantId, object payload, CancellationToken cancellationToken = default)
        => hub.Clients.Group(WhatsAppHub.TenantGroup(tenantId))
            .SendAsync("ReceiveWhatsAppEvent", payload, cancellationToken);
}
