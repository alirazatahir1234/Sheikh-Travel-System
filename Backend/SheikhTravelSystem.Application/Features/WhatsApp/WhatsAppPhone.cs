using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public static partial class WhatsAppPhone
{
    /// <summary>Digits only for Cloud API <c>to</c> field (no +).</summary>
    public static string ToApiDigits(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var digits = DigitsOnly().Replace(raw, "");
        return digits;
    }

    /// <summary>Normalize to E.164-ish with leading + when possible.</summary>
    public static string ToE164(string? raw)
    {
        var digits = ToApiDigits(raw);
        if (string.IsNullOrEmpty(digits)) return "";
        return digits.StartsWith('+') ? digits : "+" + digits;
    }

    /// <summary>
    /// Legacy helper — prefer <see cref="IWhatsAppRoutingService"/>.
    /// SheikhGo ships a single Pakistan line; all dial prefixes resolve to PK.
    /// </summary>
    public static string ResolveAccountCodeForRecipient(string? phoneE164)
    {
        _ = phoneE164;
        return "PK";
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnly();
}
