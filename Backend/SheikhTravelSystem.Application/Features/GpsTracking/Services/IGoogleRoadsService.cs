using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGoogleRoadsService
{
    Task<IReadOnlyList<SnappedGpsPoint>?> SnapToRoadsAsync(
        IReadOnlyList<LatLngPoint> points,
        CancellationToken cancellationToken = default);
}
