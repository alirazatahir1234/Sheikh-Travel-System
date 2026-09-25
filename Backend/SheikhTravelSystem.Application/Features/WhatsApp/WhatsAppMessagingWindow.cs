using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

/// <summary>
/// Meta Cloud API customer-service window: free-form replies for 24h after last inbound customer message.
/// </summary>
public static class WhatsAppMessagingWindow
{
    public static readonly TimeSpan Duration = TimeSpan.FromHours(24);
    public const int WindowSeconds = 86400;

    public static DateTime? ComputeExpiresAt(DateTime? lastIncomingMessageAtUtc)
    {
        if (lastIncomingMessageAtUtc is null)
            return null;

        var last = NormalizeUtc(lastIncomingMessageAtUtc.Value);
        return last + Duration;
    }

    public static bool IsOpen(DateTime? lastIncomingMessageAtUtc, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return IsOpenFromExpiry(ComputeExpiresAt(lastIncomingMessageAtUtc), now);
    }

    public static bool IsOpenFromStoredWindow(
        DateTime? lastIncomingMessageAtUtc,
        DateTime? windowExpiresAtUtc,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var expires = windowExpiresAtUtc ?? ComputeExpiresAt(lastIncomingMessageAtUtc);
        return IsOpenFromExpiry(expires, now);
    }

    public static WhatsAppConversationDto WithWindowFlag(WhatsAppConversationDto conversation, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var expires = conversation.WindowExpiresAt
            ?? ComputeExpiresAt(conversation.LastIncomingMessageAt);
        return conversation with
        {
            WindowExpiresAt = expires,
            IsWithinMessagingWindow = IsOpenFromExpiry(expires, now)
        };
    }

    private static bool IsOpenFromExpiry(DateTime? expiresAtUtc, DateTime nowUtc)
    {
        if (expiresAtUtc is null)
            return false;
        return nowUtc <= NormalizeUtc(expiresAtUtc.Value);
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
