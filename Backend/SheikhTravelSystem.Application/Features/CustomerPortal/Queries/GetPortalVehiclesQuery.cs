using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalVehiclesQuery : IRequest<ApiResponse<IReadOnlyList<PortalVehicleDto>>>;

public class GetPortalVehiclesQueryHandler(ICustomerPortalRepository portalRepository)
    : IRequestHandler<GetPortalVehiclesQuery, ApiResponse<IReadOnlyList<PortalVehicleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalVehicleDto>>> Handle(GetPortalVehiclesQuery request, CancellationToken cancellationToken)
    {
        var rows = await portalRepository.GetVehiclesAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<PortalVehicleDto>>.SuccessResponse(rows, "Vehicles loaded.");
    }
}
