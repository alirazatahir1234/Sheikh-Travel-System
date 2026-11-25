using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

public record GetTravelLostReasonsQuery(bool? IsActive = null) : IRequest<ApiResponse<IReadOnlyList<TravelLostReasonDto>>>;

public class GetTravelLostReasonsQueryHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelLostReasonsQuery, ApiResponse<IReadOnlyList<TravelLostReasonDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelLostReasonDto>>> Handle(GetTravelLostReasonsQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelLostReasonDto>>.SuccessResponse(await repository.GetLostReasonsAsync(
            tenantContext.GetRequiredTenantId(), request.IsActive, cancellationToken));
}
