using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Queries;

public record GetTravelChecklistsQuery(string? CountryCode = null, string? Status = null)
    : IRequest<ApiResponse<IReadOnlyList<TravelChecklistTemplateListItemDto>>>;

public class GetTravelChecklistsQueryHandler(ITravelChecklistRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelChecklistsQuery, ApiResponse<IReadOnlyList<TravelChecklistTemplateListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelChecklistTemplateListItemDto>>> Handle(GetTravelChecklistsQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelChecklistTemplateListItemDto>>.SuccessResponse(await repository.GetListAsync(
            tenantContext.GetRequiredTenantId(),
            string.IsNullOrWhiteSpace(request.CountryCode) ? null : request.CountryCode,
            Common.TravelValues.IsOneOf(request.Status, Common.TravelValues.ChecklistStatuses) ? request.Status : null,
            cancellationToken));
}
