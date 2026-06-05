namespace SheikhTravelSystem.Application.Features.CustomerPortal;

internal static class PortalPhoneHelper
{
    /// <summary>Canonical Pakistan mobile: 11 digits starting with 0 (e.g. 03001234567).</summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("92", StringComparison.Ordinal) && digits.Length >= 12)
            digits = "0" + digits[2..];
        if (digits.Length == 10 && digits[0] != '0')
            digits = "0" + digits;

        return digits;
    }

    /// <summary>Last 10 mobile digits (without leading 0) for fuzzy customer match.</summary>
    public static string MobileSuffix(string? phone)
    {
        var normalized = Normalize(phone);
        if (normalized.Length >= 11 && normalized[0] == '0')
            return normalized[1..];
        if (normalized.Length >= 10)
            return normalized[^10..];
        return normalized;
    }

    public static IReadOnlyList<string> LookupVariants(string? phone)
    {
        var trimmed = phone?.Trim() ?? string.Empty;
        var normalized = Normalize(trimmed);
        var suffix = MobileSuffix(trimmed);
        var set = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(trimmed)) set.Add(trimmed);
        if (!string.IsNullOrEmpty(normalized)) set.Add(normalized);

        if (normalized.Length == 11 && normalized[0] == '0')
        {
            var withoutZero = normalized[1..];
            set.Add(withoutZero);
            set.Add("+92" + withoutZero);
            set.Add("92" + withoutZero);
        }

        if (!string.IsNullOrEmpty(suffix) && suffix.Length == 10)
        {
            set.Add(suffix);
            set.Add("0" + suffix);
            set.Add("+92" + suffix);
            set.Add("92" + suffix);
        }

        return set.ToList();
    }
}
