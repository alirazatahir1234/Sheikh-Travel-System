namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// GPS opt-in add-ons stored in <c>TenantFeatures</c> (not <c>TenantModules</c>).
/// Core live tracking stays on the <c>GPS</c> TenantModule; these capabilities are separate toggles.
/// </summary>
public static class GpsAddOnFeatureCatalog
{
    public const string Geofencing = "geofencing";
    public const string SpeedAlerts = "speed-alerts";
    public const string RoutePlayback = "route-playback";

    public sealed record Entry(
        string FeatureKey,
        string DisplayName,
        string Description,
        string Icon,
        int SortOrder,
        bool DefaultEnabledForNewTenants);

    public static IReadOnlyList<Entry> All { get; } =
    [
        new(Geofencing, "Geofencing",
            "Geofence zones and enter/exit monitoring", "fence", 310,
            DefaultEnabledForNewTenants: false),
        new(SpeedAlerts, "Speed alerts",
            "Fixed speed-limit and road-posted speed overspeed alerts", "speed", 320,
            DefaultEnabledForNewTenants: false),
        new(RoutePlayback, "Route playback",
            "Historical route playback and trip history replay", "history", 330,
            DefaultEnabledForNewTenants: false),
    ];

    public static IReadOnlyList<string> FeatureKeys
        => All.Select(e => e.FeatureKey).ToList();

    public static bool IsGpsAddOn(string? featureKey)
        => !string.IsNullOrWhiteSpace(featureKey)
           && All.Any(e => string.Equals(e.FeatureKey, featureKey, StringComparison.OrdinalIgnoreCase));

    public static Entry? Resolve(string? featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) return null;
        return All.FirstOrDefault(e =>
            string.Equals(e.FeatureKey, featureKey, StringComparison.OrdinalIgnoreCase));
    }
}
