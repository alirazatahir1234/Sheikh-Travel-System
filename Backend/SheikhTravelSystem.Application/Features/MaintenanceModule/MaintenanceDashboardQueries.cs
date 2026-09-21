using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetMaintenanceDashboardQuery(
    DateTime? From = null,
    DateTime? To = null,
    int? BranchId = null,
    string Period = "Month",
    string Granularity = "Day")
    : IRequest<ApiResponse<MaintenanceDashboardDto>>;

public class GetMaintenanceDashboardQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceDashboardQuery, ApiResponse<MaintenanceDashboardDto>>
{
    public Task<ApiResponse<MaintenanceDashboardDto>> Handle(GetMaintenanceDashboardQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceDashboardAsync(request, cancellationToken);
}

public record GetMaintenanceAlertsQuery(int Limit = 20) : IRequest<ApiResponse<IReadOnlyList<MaintenanceAlertDto>>>;

public class GetMaintenanceAlertsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceAlertsQuery, ApiResponse<IReadOnlyList<MaintenanceAlertDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceAlertDto>>> Handle(GetMaintenanceAlertsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceAlertsAsync(request, cancellationToken);
}
