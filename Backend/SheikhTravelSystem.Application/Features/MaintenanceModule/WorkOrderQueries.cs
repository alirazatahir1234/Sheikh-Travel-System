using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetWorkOrderStatsQuery : IRequest<ApiResponse<WorkOrderStatsDto>>;

public record ListWorkOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Statuses = null,
    int? VehicleId = null,
    int? WorkshopId = null,
    string? Priority = null,
    string? Search = null)
    : IRequest<ApiResponse<PagedResult<WorkOrderListItemDto>>>;

public record GetWorkOrderByIdQuery(int Id) : IRequest<ApiResponse<WorkOrderDetailDto>>;

internal sealed record WorkOrderDetailRow(
    int Id,
    string WorkOrderNumber,
    int? RequestId,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    int? DriverId,
    string? DriverName,
    int? BranchId,
    string? BranchName,
    int? WorkshopId,
    string? WorkshopName,
    int? TechnicianId,
    string? TechnicianName,
    int? ServiceTypeId,
    string? ServiceTypeName,
    string? MaintenanceType,
    DateTime? StartDate,
    DateTime? EstimatedCompletionDate,
    DateTime? CompletedAt,
    decimal LaborCost,
    decimal PartsCost,
    decimal TotalCost,
    decimal EstimatedLaborCost,
    decimal EstimatedPartsCost,
    string Status,
    string? Priority,
    string? Notes,
    string? TechnicianNotes,
    DateTime CreatedAt);

public record ListTechniciansQuery(int? WorkshopId = null)
    : IRequest<ApiResponse<IReadOnlyList<TechnicianListItemDto>>>;

public class GetWorkOrderStatsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetWorkOrderStatsQuery, ApiResponse<WorkOrderStatsDto>>
{
    public Task<ApiResponse<WorkOrderStatsDto>> Handle(GetWorkOrderStatsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetWorkOrderStatsAsync(request, cancellationToken);
}

public class ListWorkOrdersQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListWorkOrdersQuery, ApiResponse<PagedResult<WorkOrderListItemDto>>>
{
    public Task<ApiResponse<PagedResult<WorkOrderListItemDto>>> Handle(ListWorkOrdersQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListWorkOrdersAsync(request, cancellationToken);
}

public class GetWorkOrderByIdQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetWorkOrderByIdQuery, ApiResponse<WorkOrderDetailDto>>
{
    public Task<ApiResponse<WorkOrderDetailDto>> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetWorkOrderByIdAsync(request, cancellationToken);
}

public class ListTechniciansQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListTechniciansQuery, ApiResponse<IReadOnlyList<TechnicianListItemDto>>>
{
    public Task<ApiResponse<IReadOnlyList<TechnicianListItemDto>>> Handle(ListTechniciansQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListTechniciansAsync(request, cancellationToken);
}
