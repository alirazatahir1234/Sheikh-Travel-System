using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;

namespace SheikhTravelSystem.Application.Features.Maintenance.Queries;

public record GetMaintenanceQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<MaintenanceDto>>>;

public class GetMaintenanceQueryHandler(IMaintenanceRepository maintenanceRepository)
    : IRequestHandler<GetMaintenanceQuery, ApiResponse<PagedResult<MaintenanceDto>>>
{
    public async Task<ApiResponse<PagedResult<MaintenanceDto>>> Handle(GetMaintenanceQuery request, CancellationToken cancellationToken)
    {
        var result = await maintenanceRepository.GetPagedAsync(request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<MaintenanceDto>>.SuccessResponse(result);
    }
}
