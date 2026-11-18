using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalVehiclesQuery : IRequest<ApiResponse<IReadOnlyList<PortalVehicleDto>>>;

public class GetPortalVehiclesQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalVehiclesQuery, ApiResponse<IReadOnlyList<PortalVehicleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalVehicleDto>>> Handle(GetPortalVehiclesQuery request, CancellationToken cancellationToken)
    {
        var rows = await portalRepository.GetVehiclesAsync(tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<IReadOnlyList<PortalVehicleDto>>.SuccessResponse(rows, "Vehicles loaded.");
    }
}
