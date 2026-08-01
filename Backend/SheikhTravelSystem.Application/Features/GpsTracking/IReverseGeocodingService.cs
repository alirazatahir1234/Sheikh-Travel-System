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
    string? PlaceType = null);

public interface IReverseGeocodingService
{
    /// <summary>
    /// Resolves a human-readable address for the given coordinates.
    /// Uses GpsAddressCache first; only calls the external provider on miss
    /// (or when the caller opts into forceRefresh).
    /// </summary>
    Task<ReverseGeocodeResult?> GetAddressAsync(
        double latitude,
        double longitude,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
