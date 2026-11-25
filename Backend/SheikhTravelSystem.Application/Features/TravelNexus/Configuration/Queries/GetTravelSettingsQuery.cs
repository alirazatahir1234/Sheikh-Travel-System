using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

public record GetTravelSettingsQuery : IRequest<ApiResponse<TravelSettingsDto>>;

public class GetTravelSettingsQueryHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSettingsQuery, ApiResponse<TravelSettingsDto>>
{
    public async Task<ApiResponse<TravelSettingsDto>> Handle(GetTravelSettingsQuery request, CancellationToken cancellationToken)
        => ApiResponse<TravelSettingsDto>.SuccessResponse(await repository.GetSettingsAsync(tenantContext.GetRequiredTenantId(), cancellationToken));
}
