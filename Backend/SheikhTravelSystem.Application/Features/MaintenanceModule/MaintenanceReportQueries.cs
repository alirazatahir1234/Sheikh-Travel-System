using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetMaintenanceReportQuery(
    string ReportType = "cost-analysis",
    DateTime? From = null,
    DateTime? To = null,
    int? VehicleId = null,
    int? BranchId = null,
    string? Status = null)
    : IRequest<ApiResponse<MaintenanceReportDto>>;

public class GetMaintenanceReportQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceReportQuery, ApiResponse<MaintenanceReportDto>>
{
    public Task<ApiResponse<MaintenanceReportDto>> Handle(GetMaintenanceReportQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceReportAsync(request, cancellationToken);
}
