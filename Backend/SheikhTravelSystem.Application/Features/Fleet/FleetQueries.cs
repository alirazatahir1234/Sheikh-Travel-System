using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Fleet;

public record FleetDashboardDto(
    int TotalVehicles,
    int ActiveVehicles,
    int DriversOnDuty,
    int MaintenanceDue,
    decimal MonthlyFuelCost,
    int ComplianceAlerts);

public record ComplianceDocumentDto(
    int Id,
    string EntityType,
    string? EntityName,
    string DocumentType,
    string? DocumentNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string Status,
    string? FileUrl);

public record InspectionDto(
    int Id,
    string? VehicleName,
    string? InspectedBy,
    DateTime InspectionDate,
    string Result,
    decimal? OdometerReading);

public record AssignmentDto(
    int Id,
    string? VehicleName,
    string? DriverName,
    string AssignmentType,
    string Status,
    DateTime StartAt,
    DateTime? EndAt);

public record GetFleetDashboardQuery : IRequest<ApiResponse<FleetDashboardDto>>;

public record GetComplianceDocumentsQuery : IRequest<ApiResponse<IReadOnlyList<ComplianceDocumentDto>>>;

public record GetInspectionsQuery : IRequest<ApiResponse<IReadOnlyList<InspectionDto>>>;

public record GetAssignmentsQuery : IRequest<ApiResponse<IReadOnlyList<AssignmentDto>>>;

public class GetFleetDashboardQueryHandler(IFleetRepository fleetRepository, ITenantContext tenantContext)
    : IRequestHandler<GetFleetDashboardQuery, ApiResponse<FleetDashboardDto>>
{
    public async Task<ApiResponse<FleetDashboardDto>> Handle(GetFleetDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = await fleetRepository.GetDashboardAsync(tenantId, cancellationToken);
        return ApiResponse<FleetDashboardDto>.SuccessResponse(dto);
    }
}

public class GetComplianceDocumentsQueryHandler(IFleetRepository fleetRepository, ITenantContext tenantContext)
    : IRequestHandler<GetComplianceDocumentsQuery, ApiResponse<IReadOnlyList<ComplianceDocumentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ComplianceDocumentDto>>> Handle(GetComplianceDocumentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await fleetRepository.GetComplianceDocumentsAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<ComplianceDocumentDto>>.SuccessResponse(rows);
    }
}

public class GetInspectionsQueryHandler(IFleetRepository fleetRepository, ITenantContext tenantContext)
    : IRequestHandler<GetInspectionsQuery, ApiResponse<IReadOnlyList<InspectionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<InspectionDto>>> Handle(GetInspectionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await fleetRepository.GetInspectionsAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<InspectionDto>>.SuccessResponse(rows);
    }
}

public class GetAssignmentsQueryHandler(IFleetRepository fleetRepository, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentsQuery, ApiResponse<IReadOnlyList<AssignmentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignmentDto>>> Handle(GetAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await fleetRepository.GetAssignmentsAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<AssignmentDto>>.SuccessResponse(rows);
    }
}
