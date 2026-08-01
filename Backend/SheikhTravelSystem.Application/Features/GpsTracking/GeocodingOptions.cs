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
    /// Optional Google Maps Platform key (Geocoding + Places Nearby).
    /// When set, reverse geocode returns street-level addresses and nearby shop/POI names.
    /// Prefer env <c>Geocoding__GoogleMapsApiKey</c> in production.
    /// </summary>
    public string? GoogleMapsApiKey { get; set; }
}
