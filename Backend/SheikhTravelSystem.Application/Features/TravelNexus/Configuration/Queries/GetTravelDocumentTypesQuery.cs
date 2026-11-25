using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

public record GetTravelDocumentTypesQuery(bool? IsActive = null) : IRequest<ApiResponse<IReadOnlyList<TravelDocumentTypeDto>>>;

public class GetTravelDocumentTypesQueryHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelDocumentTypesQuery, ApiResponse<IReadOnlyList<TravelDocumentTypeDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelDocumentTypeDto>>> Handle(GetTravelDocumentTypesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelDocumentTypeDto>>.SuccessResponse(await repository.GetDocumentTypesAsync(
            tenantContext.GetRequiredTenantId(), request.IsActive, cancellationToken));
}
