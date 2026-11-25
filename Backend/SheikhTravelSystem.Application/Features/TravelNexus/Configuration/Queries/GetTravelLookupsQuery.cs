using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

/// <summary>Branches and staff for TravelNexus dropdowns, without requiring platform admin permissions.</summary>
public record GetTravelLookupsQuery : IRequest<ApiResponse<TravelLookupsDto>>;

public class GetTravelLookupsQueryHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelLookupsQuery, ApiResponse<TravelLookupsDto>>
{
    public async Task<ApiResponse<TravelLookupsDto>> Handle(GetTravelLookupsQuery request, CancellationToken cancellationToken)
        => ApiResponse<TravelLookupsDto>.SuccessResponse(
            await repository.GetLookupsAsync(tenantContext.GetRequiredTenantId(), cancellationToken));
}
