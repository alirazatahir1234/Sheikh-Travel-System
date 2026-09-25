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
    /// Matches default GCC → UAE / Pakistan → PK routing.
    /// </summary>
    public static string ResolveAccountCodeForRecipient(string? phoneE164)
    {
        var digits = ToApiDigits(phoneE164);
        if (digits.StartsWith("92", StringComparison.Ordinal)) return "PK";
        if (digits.StartsWith("971", StringComparison.Ordinal)
            || digits.StartsWith("966", StringComparison.Ordinal)
            || digits.StartsWith("974", StringComparison.Ordinal)
            || digits.StartsWith("968", StringComparison.Ordinal)
            || digits.StartsWith("965", StringComparison.Ordinal)
            || digits.StartsWith("973", StringComparison.Ordinal))
            return "UAE";
        return "UAE";
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnly();
}
