namespace SheikhTravelSystem.Application.Features.TravelNexus.Common;

/// <summary>
/// Masking for traveler identity documents. Full values are returned only to callers holding
/// <see cref="Application.Common.TravelPermissions.TravelerViewPii"/>. Never log these values.
/// </summary>
public static class TravelPii
{
    public const char MaskChar = '•';

    /// <summary>Keeps the last <paramref name="visible"/> characters, masks the rest.</summary>
    public static string? Mask(string? value, int visible = 3)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= visible) return new string(MaskChar, value.Length);
        return new string(MaskChar, value.Length - visible) + value[^visible..];
    }

    /// <summary>True when the value looks like a masked value sent back by a client.</summary>
    public static bool IsMasked(string? value) => value is not null && value.Contains(MaskChar);

    /// <summary>
    /// Passport status for display and reminders. <paramref name="today"/> is a UTC date.
    /// </summary>
    public static string PassportStatus(string? passportNumber, DateTime? expiryDate, DateTime today, int warningDays)
    {
        if (string.IsNullOrWhiteSpace(passportNumber) || expiryDate is null) return "Missing";
        var expiry = expiryDate.Value.Date;
        if (expiry < today.Date) return "Expired";
        if (expiry <= today.Date.AddDays(warningDays)) return "ExpiringSoon";
        return "Valid";
    }
}
