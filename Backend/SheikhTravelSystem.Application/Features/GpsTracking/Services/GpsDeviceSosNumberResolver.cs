using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Resolves the human SOS / center number for device offline SMS.
/// Never invents numbers. Device SIM (<c>GpsDevices.Phone</c>) is not used as SOS recipient.
/// </summary>
public static partial class GpsDeviceSosNumberResolver
{
    /// <summary>
    /// Preference: <paramref name="sosNumber"/> → phone-like <paramref name="contact"/> → null.
    /// </summary>
    public static string? Resolve(string? sosNumber, string? contact, string? deviceSimPhone = null)
    {
        _ = deviceSimPhone; // intentionally unused — SIM is the tracker's own number, not SOS

        var sos = NormalizePhone(sosNumber);
        if (sos is not null)
            return sos;

        return NormalizePhone(contact);
    }

    public static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        // Reject free-text contacts like "Ali / vendor" without enough digits
        var digits = DigitOnly().Replace(trimmed, "");
        if (digits.Length < 7)
            return null;

        // Keep leading + if present; otherwise keep original trimmed (country code already baked in)
        if (trimmed.StartsWith('+'))
            return "+" + digits;

        return digits.Length == trimmed.Length ? trimmed : digits;
    }

    public static string Mask(string phone)
    {
        var digits = DigitOnly().Replace(phone, "");
        if (digits.Length <= 4)
            return "****";
        return new string('*', Math.Max(digits.Length - 4, 0)) + digits[^4..];
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitOnly();
}
