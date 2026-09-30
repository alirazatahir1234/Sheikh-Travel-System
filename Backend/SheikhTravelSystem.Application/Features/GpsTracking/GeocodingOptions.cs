namespace SheikhTravelSystem.Application.Features.GpsTracking;

public class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Nominatim's usage policy requires a real identifying User-Agent, or requests get blocked:
    /// https://operations.osmfoundation.org/policies/nominatim/
    /// </summary>
    public string UserAgent { get; set; } = "SheikhGoERP/1.0";

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    /// <summary>
    /// Optional Google Maps Platform key (Geocoding; Places Nearby only when
    /// <see cref="IncludeNearbyPlace"/> is true). Prefer env
    /// <c>Geocoding__GoogleMapsApiKey</c> in production.
    /// </summary>
    public string? GoogleMapsApiKey { get; set; }

    /// <summary>
    /// When true, background GPS backfill may call Google after Nominatim miss/coarse.
    /// Default false — use Nominatim for ingest/backfill to avoid Maps bills.
    /// Explicit UI reverse-geocode can still request Google via allowGoogle.
    /// </summary>
    public bool PreferGoogle { get; set; }

    /// <summary>
    /// When true, reverse geocode also calls Places Nearby Search (legacy) for POI names.
    /// Default false — Places is billable and was the main companion cost to Geocoding.
    /// </summary>
    public bool IncludeNearbyPlace { get; set; }

    /// <summary>
    /// Minimum seconds between background reverse-geocode jobs for the same vehicle.
    /// </summary>
    public int BackfillCooldownoldownSeconds { get; set; } = 300;
}
