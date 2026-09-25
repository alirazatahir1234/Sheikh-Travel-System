using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Stable codes for Nearby Search soft failures (UI / ops; never log API keys).</summary>
public static class NearbyPlacesErrorCodes
{
    public const string Configuration = "Configuration";
    public const string Authentication = "Authentication";
    public const string GoogleApi = "GoogleApi";
    public const string InvalidRequest = "InvalidRequest";
}

/// <summary>
/// Outcome of Places Nearby Search. <see cref="Places"/> is null on soft failure;
/// <see cref="ErrorMessage"/> explains why (key / API / restriction).
/// </summary>
public sealed record NearbyPlacesSearchResult(
    IReadOnlyList<NearbyPlaceDto>? Places,
    string? ErrorMessage = null,
    string? ErrorCode = null)
{
    public static NearbyPlacesSearchResult Ok(IReadOnlyList<NearbyPlaceDto> places)
        => new(places);

    public static NearbyPlacesSearchResult Fail(string message, string? errorCode = null)
        => new(null, message, errorCode);
}

public interface IGooglePlacesNearbyService
{
    /// <summary>
    /// Places API (New) Nearby Search around a GPS fix.
    /// </summary>
    Task<NearbyPlacesSearchResult> SearchNearbyAsync(
        double latitude,
        double longitude,
        string category,
        int radiusMeters = 1500,
        int maxResults = 8,
        CancellationToken cancellationToken = default);
}
