using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

/// <summary>
/// Meta Cloud API customer-service window: free-form replies for 24h after last inbound customer message.
/// </summary>
public static class WhatsAppMessagingWindow
{
    public static readonly TimeSpan Duration = TimeSpan.FromHours(24);
    public const int WindowSeconds = 86400;

    public static bool IsOpen(DateTime? lastIncomingMessageAtUtc, DateTime? utcNow = null)
    {
        if (lastIncomingMessageAtUtc is null)
            return false;

        var now = utcNow ?? DateTime.UtcNow;
        var last = lastIncomingMessageAtUtc.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(lastIncomingMessageAtUtc.Value, DateTimeKind.Utc)
            : lastIncomingMessageAtUtc.Value.ToUniversalTime();

        return now - last <= Duration;
    }

    public static WhatsAppConversationDto WithWindowFlag(WhatsAppConversationDto conversation, DateTime? utcNow = null)
        => conversation with
        {
            IsWithinMessagingWindow = IsOpen(conversation.LastIncomingMessageAt, utcNow ?? DateTime.UtcNow)
        };
}
