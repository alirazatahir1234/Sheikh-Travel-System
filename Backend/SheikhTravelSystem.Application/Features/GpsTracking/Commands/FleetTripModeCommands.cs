using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record UpsertFleetTripModeSettingsCommand(UpsertFleetTripModeSettingsRequest Body)
    : IRequest<ApiResponse<FleetTripModeSettingsDto>>;

public class UpsertFleetTripModeSettingsCommandHandler(
    IFleetTripModeRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<UpsertFleetTripModeSettingsCommand, ApiResponse<FleetTripModeSettingsDto>>
{
    public async Task<ApiResponse<FleetTripModeSettingsDto>> Handle(
        UpsertFleetTripModeSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var settings = await repository.UpsertSettingsAsync(tenantId, request.Body, cancellationToken);
        return ApiResponse<FleetTripModeSettingsDto>.SuccessResponse(settings, "Trip creation mode saved.");
    }
}
