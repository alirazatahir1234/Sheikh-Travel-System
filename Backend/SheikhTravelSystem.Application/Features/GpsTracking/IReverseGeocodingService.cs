namespace SheikhTravelSystem.Application.Features.GpsTracking;

public record ReverseGeocodeResult(
    string FormattedAddress,
    string? Road = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? PostalCode = null,
    bool FromCache = false,
    /// <summary>Nearest shop / landmark / POI name when available.</summary>
    string? PlaceName = null,
    /// <summary>Google/OSM place type (e.g. store, mosque, premise).</summary>
    string? PlaceType = null,
    /// <summary>
    /// Best operator-facing first line (street/locality) for list cards.
    /// </summary>
    string? PrimaryAddress = null,
    /// <summary>
    /// Nearby POI/landmark label when confidence is adequate.
    /// </summary>
    string? NearbyPlaceName = null,
    /// <summary>
    /// City/state/country summary line.
    /// </summary>
    string? LocalityLine = null,
    /// <summary>
    /// Address confidence tier: exact | nearby | coarse
    /// </summary>
    string AddressQuality = "coarse");

public interface IReverseGeocodingService
{
    /// <summary>
    /// Resolves a human-readable address for the given coordinates.
    /// Uses GpsAddressCache first; only calls the external provider on miss
    /// (or when the caller opts into forceRefresh).
    /// </summary>
    /// <param name="allowGoogle">
    /// When true (explicit UI lookup), Google Geocoding may be used even if
    /// <c>Geocoding:PreferGoogle</c> is false. Background callers should leave this false.
    /// </param>
    Task<ReverseGeocodeResult?> GetAddressAsync(
        double latitude,
        double longitude,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default,
        bool allowGoogle = false);

    /// <summary>
    /// Whatever address GpsAddressCache holds for these coordinates, or null. Unlike the cache step of
    /// <see cref="GetAddressAsync"/> it also returns an old or district-only answer: for display any
    /// cached name beats coordinates. Never calls a provider, so a request can afford it for every
    /// place it shows.
    /// </summary>
    Task<ReverseGeocodeResult?> GetCachedAddressAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
