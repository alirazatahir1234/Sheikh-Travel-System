using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGoogleRoadsService
{
    Task<IReadOnlyList<SnappedGpsPoint>?> SnapToRoadsAsync(
        IReadOnlyList<LatLngPoint> points,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Google Roads API <c>speedLimits</c> for a single GPS fix (units KPH).
    /// Returns null when key missing, API error, or no limit data.
    /// Requires Roads API + Speed Limits product access in Google Cloud.
    /// </summary>
    Task<GoogleRoadSpeedLimitDto?> GetSpeedLimitAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}

/// <param name="SpeedLimitKmh">Posted speed limit in km/h.</param>
/// <param name="PlaceId">Snapped road segment placeId when provided.</param>
public sealed record GoogleRoadSpeedLimitDto(decimal SpeedLimitKmh, string? PlaceId);
