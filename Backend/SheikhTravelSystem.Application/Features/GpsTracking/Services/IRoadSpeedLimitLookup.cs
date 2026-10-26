namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Resolves the posted road speed limit (km/h) for a GPS fix, with caching to minimize Google Roads API cost.
/// </summary>
public interface IRoadSpeedLimitLookup
{
    /// <summary>
    /// Returns the posted speed limit in km/h, or null when unknown / API unavailable / circuit open.
    /// Never throws for expected API/config failures.
    /// </summary>
    Task<RoadSpeedLimitResult?> GetSpeedLimitKmhAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

/// <param name="SpeedLimitKmh">Posted limit in km/h.</param>
/// <param name="PlaceId">Google placeId when returned by Roads API.</param>
/// <param name="CacheKey">Key used for durable/memory cache.</param>
/// <param name="FromCache">True when served without a live Roads API call.</param>
public sealed record RoadSpeedLimitResult(
    decimal SpeedLimitKmh,
    string? PlaceId,
    string CacheKey,
    bool FromCache);
