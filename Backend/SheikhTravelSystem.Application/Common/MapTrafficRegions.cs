namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Operational regions where Nexus uses Google Maps live traffic (Routes API TRAFFIC_AWARE).
/// </summary>
public static class MapTrafficRegions
{
    public const string Uae = "AE";
    public const string Pakistan = "PK";
    public const string SaudiArabia = "SA";

    public static readonly IReadOnlyList<string> TrafficSupportedRegions = [Uae, Pakistan, SaudiArabia];

    /// <summary>Infer AE (Dubai/UAE), PK, or SA from coordinates. More specific boxes win.</summary>
    public static string InferRegionCode(double latitude, double longitude)
    {
        if (latitude is >= 22.4 and <= 26.6 && longitude is >= 51.0 and <= 57.5)
            return Uae;
        if (latitude is >= 23.4 and <= 37.5 && longitude is >= 60.5 and <= 78.5)
            return Pakistan;
        if (latitude is >= 16.0 and <= 32.5 && longitude is >= 34.5 and <= 55.7)
            return SaudiArabia;
        return Pakistan;
    }

    public static bool SupportsLiveTraffic(string regionCode) =>
        TrafficSupportedRegions.Contains(regionCode, StringComparer.OrdinalIgnoreCase);
}
