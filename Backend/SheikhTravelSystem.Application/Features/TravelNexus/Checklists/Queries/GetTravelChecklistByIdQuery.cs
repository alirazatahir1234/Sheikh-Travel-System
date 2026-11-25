using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Queries;

public record GetTravelChecklistByIdQuery(int Id) : IRequest<ApiResponse<TravelChecklistTemplateDto>>;

public class GetTravelChecklistByIdQueryHandler(TravelChecklistGuard guard, ITenantContext tenantContext)
    : IRequestHandler<GetTravelChecklistByIdQuery, ApiResponse<TravelChecklistTemplateDto>>
{
    public async Task<ApiResponse<TravelChecklistTemplateDto>> Handle(GetTravelChecklistByIdQuery request, CancellationToken cancellationToken)
        => ApiResponse<TravelChecklistTemplateDto>.SuccessResponse(
            await guard.GetAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken));
}
