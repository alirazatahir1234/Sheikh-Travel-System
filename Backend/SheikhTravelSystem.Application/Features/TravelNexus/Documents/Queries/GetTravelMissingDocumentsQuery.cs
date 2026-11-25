using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents.Queries;

/// <summary>Open cases with outstanding mandatory documents (source for follow-ups and Phase 7 reminders).</summary>
public class GetTravelMissingDocumentsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelMissingDocumentsItemDto>>>;

public class GetTravelMissingDocumentsQueryHandler(
    ITravelDocumentRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelMissingDocumentsQuery, ApiResponse<PagedResult<TravelMissingDocumentsItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelMissingDocumentsItemDto>>> Handle(GetTravelMissingDocumentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        return ApiResponse<PagedResult<TravelMissingDocumentsItemDto>>.SuccessResponse(
            await repository.GetMissingAsync(tenantId, scope, request.Page, request.PageSize, cancellationToken));
    }
}
