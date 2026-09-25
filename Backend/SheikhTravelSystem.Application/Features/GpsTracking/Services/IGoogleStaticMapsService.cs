using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGoogleStaticMapsService
{
    string? BuildStaticMapUrl(StaticMapRequestDto request);
}
