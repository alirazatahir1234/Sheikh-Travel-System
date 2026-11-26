using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Application.Features.Vehicles;

/// <summary>
/// One plate format at entry: trimmed, upper-case, with any run of spaces, hyphens or
/// underscores as a single hyphen ("bwy 217" → "BWY-217"). Stops the same fleet mixing
/// "LEC-8825" and "BWY 217". Existing rows are not rewritten.
/// The ERP applies the same rule on blur (normalizePlateNumber in plate-number.util.ts).
/// </summary>
public static partial class PlateNumberNormalizer
{
    [GeneratedRegex(@"[\s\-_‐-―]+")]
    private static partial Regex Separators();

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? string.Empty;
        return Separators().Replace(value.Trim(), "-").Trim('-').ToUpperInvariant();
    }
}
