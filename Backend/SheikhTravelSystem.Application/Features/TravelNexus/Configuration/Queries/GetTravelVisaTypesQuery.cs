using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

public record GetTravelVisaTypesQuery(string? CountryCode = null, bool? IsActive = null)
    : IRequest<ApiResponse<IReadOnlyList<TravelVisaTypeDto>>>;

public class GetTravelVisaTypesQueryHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelVisaTypesQuery, ApiResponse<IReadOnlyList<TravelVisaTypeDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelVisaTypeDto>>> Handle(GetTravelVisaTypesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelVisaTypeDto>>.SuccessResponse(await repository.GetVisaTypesAsync(
            tenantContext.GetRequiredTenantId(), request.CountryCode, request.IsActive, cancellationToken));
}
