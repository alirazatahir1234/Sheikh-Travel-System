namespace SheikhTravelSystem.Application.Features.WhatsApp;

/// <summary>Forward-only delivery status ranking: Queued → Sent → Delivered → Read. Failed is terminal.</summary>
public static class WhatsAppMessageStatusPrecedence
{
    public static bool ShouldApply(string? currentStatus, string incomingStatus)
    {
        var incoming = Normalize(incomingStatus);
        if (incoming is null)
            return false;

        var current = Normalize(currentStatus);
        if (current is null)
            return true;

        // Failed is terminal for that attempt — ignore further Meta updates.
        if (current == "failed")
            return false;

        if (incoming == "failed")
            return true;

        return Rank(incoming) > Rank(current);
    }

    public static int Rank(string normalizedStatus) => normalizedStatus switch
    {
        "queued" or "sending" => 1,
        "sent" => 2,
        "delivered" => 3,
        "read" => 4,
        "failed" => 100,
        _ => 0
    };

    public static string? Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        return status.Trim().ToLowerInvariant() switch
        {
            "queued" => "queued",
            "sending" => "sending",
            "sent" => "sent",
            "delivered" => "delivered",
            "read" => "read",
            "failed" => "failed",
            "received" => "received",
            _ => status.Trim().ToLowerInvariant()
        };
    }

    public static string ToPersisted(string normalized) => normalized switch
    {
        "queued" => "Queued",
        "sending" => "Sending",
        "sent" => "Sent",
        "delivered" => "Delivered",
        "read" => "Read",
        "failed" => "Failed",
        _ => normalized
    };
}
