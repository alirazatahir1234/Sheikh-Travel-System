using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;

namespace SheikhTravelSystem.Application.Features.Maintenance.Queries;

public record GetMaintenanceByIdQuery(int Id) : IRequest<ApiResponse<MaintenanceDto>>;

public class GetMaintenanceByIdQueryHandler(IMaintenanceRepository maintenanceRepository, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceByIdQuery, ApiResponse<MaintenanceDto>>
{
    public async Task<ApiResponse<MaintenanceDto>> Handle(GetMaintenanceByIdQuery request, CancellationToken cancellationToken)
    {
        var maintenance = await maintenanceRepository.GetByIdAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<MaintenanceDto>.SuccessResponse(maintenance);
    }
}
