using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Renders versioned command templates with {{param}} placeholders. Pure — unit-testable without DB.
/// </summary>
public static class GpsCommandTemplateRenderer
{
    private static readonly Regex Placeholder = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public static string Render(string payloadTemplate, IReadOnlyDictionary<string, string>? parameters)
    {
        if (string.IsNullOrEmpty(payloadTemplate))
            return string.Empty;

        return Placeholder.Replace(payloadTemplate, match =>
        {
            var key = match.Groups[1].Value;
            if (parameters is not null && parameters.TryGetValue(key, out var value))
                return value ?? string.Empty;
            return string.Empty;
        });
    }

    /// <summary>
    /// Picks the best active template for a firmware hint. Prefer exact firmware range match;
    /// otherwise the null-range (default) template with highest TemplateVersion.
    /// </summary>
    public static T? ResolveBestTemplate<T>(
        IEnumerable<T> candidates,
        string? firmwareVersion,
        Func<T, string?> firmwareMin,
        Func<T, string?> firmwareMax,
        Func<T, int> version)
    {
        var list = candidates.ToList();
        if (list.Count == 0) return default;

        if (!string.IsNullOrWhiteSpace(firmwareVersion))
        {
            var ranged = list
                .Where(t =>
                {
                    var min = firmwareMin(t);
                    var max = firmwareMax(t);
                    if (min is null && max is null) return false;
                    return FirmwareInRange(firmwareVersion, min, max);
                })
                .OrderByDescending(version)
                .FirstOrDefault();

            if (ranged is not null)
                return ranged;
        }

        return list
            .Where(t => firmwareMin(t) is null && firmwareMax(t) is null)
            .OrderByDescending(version)
            .FirstOrDefault()
            ?? list.OrderByDescending(version).First();
    }

    public static bool FirmwareInRange(string firmware, string? min, string? max)
    {
        // Lexicographic compare is good enough for seeded hints like "1.0+" / "2.1";
        // numeric SemVer not required for v1 resolver.
        if (min is not null && string.CompareOrdinal(firmware, min) < 0)
            return false;
        if (max is not null && string.CompareOrdinal(firmware, max) > 0)
            return false;
        return true;
    }
}
