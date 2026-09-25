using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsFleetHealthCalculator
{
    Task<FleetHealthSummaryDto> ComputeAsync(CancellationToken cancellationToken = default);
}
