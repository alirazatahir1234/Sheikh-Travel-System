using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGoogleRoutesService
{
    Task<GoogleRouteResult?> ComputeRouteAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        IReadOnlyList<LatLngPoint>? waypoints = null,
        CancellationToken cancellationToken = default);
}
