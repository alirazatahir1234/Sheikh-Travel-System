using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetFleetTripModeSettingsQuery : IRequest<ApiResponse<FleetTripModeSettingsDto>>;

public class GetFleetTripModeSettingsQueryHandler(
    IFleetTripModeRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetFleetTripModeSettingsQuery, ApiResponse<FleetTripModeSettingsDto>>
{
    public async Task<ApiResponse<FleetTripModeSettingsDto>> Handle(
        GetFleetTripModeSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var settings = await repository.GetSettingsAsync(tenantId, cancellationToken);
        return ApiResponse<FleetTripModeSettingsDto>.SuccessResponse(settings);
    }
}
