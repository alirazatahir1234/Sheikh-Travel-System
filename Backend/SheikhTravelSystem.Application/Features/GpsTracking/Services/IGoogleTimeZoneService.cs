using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGoogleTimeZoneService
{
    Task<GoogleTimeZoneResult?> GetTimeZoneAsync(
        double latitude,
        double longitude,
        DateTime? timestampUtc = null,
        CancellationToken cancellationToken = default);
}
