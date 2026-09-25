using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGoogleRouteOptimizationService
{
    Task<OptimizeDispatchToursResultDto?> OptimizeToursAsync(
        OptimizeDispatchToursRequestDto request,
        CancellationToken cancellationToken = default);
}
