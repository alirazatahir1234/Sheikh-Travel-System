using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

public record GetTravelLeadSourcesQuery(bool? IsActive = null) : IRequest<ApiResponse<IReadOnlyList<TravelLeadSourceDto>>>;

public class GetTravelLeadSourcesQueryHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelLeadSourcesQuery, ApiResponse<IReadOnlyList<TravelLeadSourceDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelLeadSourceDto>>> Handle(GetTravelLeadSourcesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelLeadSourceDto>>.SuccessResponse(await repository.GetLeadSourcesAsync(
            tenantContext.GetRequiredTenantId(), request.IsActive, cancellationToken));
}
