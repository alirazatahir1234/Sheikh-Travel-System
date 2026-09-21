using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

// ── Phase 4: History & Reports ────────────────────────────────────────────────

public record GetWorkshopVendorStatsQuery() : IRequest<ApiResponse<WorkshopVendorStatsDto>>;

public class GetWorkshopVendorStatsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetWorkshopVendorStatsQuery, ApiResponse<WorkshopVendorStatsDto>>
{
    public Task<ApiResponse<WorkshopVendorStatsDto>> Handle(GetWorkshopVendorStatsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetWorkshopVendorStatsAsync(request, cancellationToken);
}

public record GetMaintenanceHistoryQuery(
    int? VehicleId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? ServiceType = null,
    decimal? MinCost = null,
    decimal? MaxCost = null)
    : IRequest<ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>>;

public class GetMaintenanceHistoryQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceHistoryQuery, ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>>
{
    public Task<ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>> Handle(GetMaintenanceHistoryQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceHistoryAsync(request, cancellationToken);
}
