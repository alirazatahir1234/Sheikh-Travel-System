namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Identifies Gmeet trackers. SheikhGo had no prior "Gmeet" catalog entry —
/// matching uses catalog keys, brand/vendor/name/model text, plus optional config patterns.
/// </summary>
public static class GmeetDeviceMatcher
{
    public const string BrandName = "Gmeet";
    public const string CatalogKeyPrefix = "gmeet_";
    public const string DefaultCatalogKey = "gmeet_generic";

    public static bool IsGmeet(
        string? catalogKey,
        string? trackerModelKey,
        string? brandName,
        string? vendor,
        string? model,
        string? name,
        IEnumerable<string>? extraNameContains = null)
    {
        if (StartsWithPrefix(catalogKey, CatalogKeyPrefix) ||
            StartsWithPrefix(trackerModelKey, CatalogKeyPrefix))
            return true;

        if (ContainsToken(brandName, "gmeet") ||
            ContainsToken(vendor, "gmeet") ||
            ContainsToken(model, "gmeet") ||
            ContainsToken(name, "gmeet"))
            return true;

        if (extraNameContains is null)
            return false;

        foreach (var token in extraNameContains)
        {
            if (string.IsNullOrWhiteSpace(token))
                continue;
            if (ContainsToken(catalogKey, token) ||
                ContainsToken(trackerModelKey, token) ||
                ContainsToken(brandName, token) ||
                ContainsToken(vendor, token) ||
                ContainsToken(model, token) ||
                ContainsToken(name, token))
                return true;
        }

        return false;
    }

    private static bool StartsWithPrefix(string? value, string prefix) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsToken(string? value, string token) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
