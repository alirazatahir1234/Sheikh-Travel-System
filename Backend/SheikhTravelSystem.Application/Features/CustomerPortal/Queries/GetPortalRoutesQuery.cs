using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalRoutesQuery : IRequest<ApiResponse<IReadOnlyList<PortalRouteDto>>>;

public class GetPortalRoutesQueryHandler(ICustomerPortalRepository portalRepository, ITenantContext tenantContext)
    : IRequestHandler<GetPortalRoutesQuery, ApiResponse<IReadOnlyList<PortalRouteDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalRouteDto>>> Handle(GetPortalRoutesQuery request, CancellationToken cancellationToken)
    {
        var rows = await portalRepository.GetRoutesAsync(tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<IReadOnlyList<PortalRouteDto>>.SuccessResponse(rows, "Routes loaded.");
    }
}
