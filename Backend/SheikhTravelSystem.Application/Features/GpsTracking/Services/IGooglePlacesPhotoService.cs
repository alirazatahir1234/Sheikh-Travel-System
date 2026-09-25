using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGooglePlacesPhotoService
{
    /// <summary>
    /// Resolves a Places API (New) photo resource to a browser <c>photoUri</c>.
    /// Soft-fails to null when the key is missing, the name is invalid, or Google returns an error.
    /// </summary>
    Task<NearbyPlacePhotoDto?> ResolveMediaAsync(
        string photoResourceName,
        int maxWidthPx = 800,
        CancellationToken cancellationToken = default);
}
