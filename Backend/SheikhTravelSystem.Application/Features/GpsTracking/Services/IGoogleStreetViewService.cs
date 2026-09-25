using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGoogleStreetViewService
{
    string? BuildStreetViewUrl(StreetViewRequestDto request);
}
