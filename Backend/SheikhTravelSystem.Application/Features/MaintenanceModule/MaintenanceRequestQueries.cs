using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record ListMaintenanceRequestsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    int? VehicleId = null,
    string? Search = null)
    : IRequest<ApiResponse<PagedResult<MaintenanceRequestDto>>>;

public record GetMaintenanceRequestByIdQuery(int Id) : IRequest<ApiResponse<MaintenanceRequestDto>>;

public class ListMaintenanceRequestsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListMaintenanceRequestsQuery, ApiResponse<PagedResult<MaintenanceRequestDto>>>
{
    public Task<ApiResponse<PagedResult<MaintenanceRequestDto>>> Handle(ListMaintenanceRequestsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListMaintenanceRequestsAsync(request, cancellationToken);
}

public class GetMaintenanceRequestByIdQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceRequestByIdQuery, ApiResponse<MaintenanceRequestDto>>
{
    public Task<ApiResponse<MaintenanceRequestDto>> Handle(GetMaintenanceRequestByIdQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceRequestByIdAsync(request, cancellationToken);
}
