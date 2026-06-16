using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

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

public class GetFleetDashboardQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetFleetDashboardQuery, ApiResponse<FleetDashboardDto>>
{
    public async Task<ApiResponse<FleetDashboardDto>> Handle(GetFleetDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var dto = await connection.QuerySingleAsync<FleetDashboardDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) AS TotalVehicles,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (1, 2)) AS ActiveVehicles,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (1, 2)) AS DriversOnDuty,
                (SELECT COUNT(*) FROM Maintenance m INNER JOIN Vehicles v ON m.VehicleId = v.Id
                    WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId AND m.Status IN (1, 2)) AS MaintenanceDue,
                (SELECT ISNULL(SUM(f.TotalCost), 0) FROM FuelLogs f INNER JOIN Vehicles v ON f.VehicleId = v.Id
                    WHERE f.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND f.FuelDate >= DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1)) AS MonthlyFuelCost,
                (SELECT COUNT(*) FROM ComplianceDocuments WHERE IsDeleted = 0 AND TenantId = @TenantId
                    AND ExpiryDate IS NOT NULL AND ExpiryDate <= DATEADD(DAY, 30, GETUTCDATE())) AS ComplianceAlerts
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<FleetDashboardDto>.SuccessResponse(dto);
    }
}

public class GetComplianceDocumentsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetComplianceDocumentsQuery, ApiResponse<IReadOnlyList<ComplianceDocumentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ComplianceDocumentDto>>> Handle(GetComplianceDocumentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<ComplianceDocumentDto>(new CommandDefinition("""
            SELECT c.Id, c.EntityType,
                   CASE WHEN c.EntityType = 'Vehicle' THEN v.Name ELSE d.FullName END AS EntityName,
                   c.DocumentType, c.DocumentNumber, c.IssuedDate, c.ExpiryDate, c.Status, c.FileUrl
            FROM ComplianceDocuments c
            LEFT JOIN Vehicles v ON c.EntityType = 'Vehicle' AND c.EntityId = v.Id
            LEFT JOIN Drivers d ON c.EntityType = 'Driver' AND c.EntityId = d.Id
            WHERE c.IsDeleted = 0 AND c.TenantId = @TenantId
            ORDER BY c.ExpiryDate ASC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<ComplianceDocumentDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetInspectionsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetInspectionsQuery, ApiResponse<IReadOnlyList<InspectionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<InspectionDto>>> Handle(GetInspectionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<InspectionDto>(new CommandDefinition("""
            SELECT i.Id, v.Name AS VehicleName, i.InspectedBy, i.InspectionDate, i.Result, i.OdometerReading
            FROM Inspections i
            INNER JOIN Vehicles v ON i.VehicleId = v.Id
            WHERE i.IsDeleted = 0 AND v.TenantId = @TenantId
            ORDER BY i.InspectionDate DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<InspectionDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetAssignmentsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentsQuery, ApiResponse<IReadOnlyList<AssignmentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignmentDto>>> Handle(GetAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<AssignmentDto>(new CommandDefinition("""
            SELECT a.Id, v.Name AS VehicleName, d.FullName AS DriverName,
                   a.AssignmentType, a.Status, a.StartAt, a.EndAt
            FROM AssignmentHistory a
            INNER JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Drivers d ON a.DriverId = d.Id
            WHERE a.IsDeleted = 0 AND v.TenantId = @TenantId
            ORDER BY a.StartAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<AssignmentDto>>.SuccessResponse(rows.ToList());
    }
}
