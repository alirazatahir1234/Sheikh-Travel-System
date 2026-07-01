using Dapper;
using MediatR;
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

public class GetWorkOrderStatsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWorkOrderStatsQuery, ApiResponse<WorkOrderStatsDto>>
{
    public async Task<ApiResponse<WorkOrderStatsDto>> Handle(
        GetWorkOrderStatsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var stats = await connection.QuerySingleAsync<WorkOrderStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status IN (N'Draft', N'Open', N'Assigned')) AS [Open],
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status IN (N'InProgress', N'WaitingParts')) AS [InProgress],
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status IN (N'Completed', N'Closed')) AS [Completed],
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status = N'Cancelled') AS [Cancelled]
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WorkOrderStatsDto>.SuccessResponse(stats);
    }
}

public class ListWorkOrdersQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListWorkOrdersQuery, ApiResponse<PagedResult<WorkOrderListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<WorkOrderListItemDto>>> Handle(
        ListWorkOrdersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();
        var offset = (Math.Max(1, request.Page) - 1) * request.PageSize;

        var clauses = new List<string> { "wo.IsDeleted = 0", "wo.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Offset", offset);
        p.Add("PageSize", request.PageSize);

        if (!string.IsNullOrWhiteSpace(request.Statuses))
        {
            var statuses = request.Statuses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (statuses.Length > 0)
            {
                clauses.Add("wo.Status IN @Statuses");
                p.Add("Statuses", statuses);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Status))
        {
            clauses.Add("wo.Status = @Status");
            p.Add("Status", request.Status.Trim());
        }
        if (request.VehicleId.HasValue)
        {
            clauses.Add("wo.VehicleId = @VehicleId");
            p.Add("VehicleId", request.VehicleId.Value);
        }
        if (request.WorkshopId.HasValue)
        {
            clauses.Add("wo.WorkshopId = @WorkshopId");
            p.Add("WorkshopId", request.WorkshopId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            clauses.Add("wo.Priority = @Priority");
            p.Add("Priority", request.Priority.Trim());
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            clauses.Add("(wo.WorkOrderNumber LIKE @Search OR v.Name LIKE @Search OR wo.ServiceTypeName LIKE @Search)");
            p.Add("Search", $"%{request.Search.Trim()}%");
        }

        var where = string.Join(" AND ", clauses);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {MaintenanceSql.WorkOrderListFrom} WHERE {where}", p, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<WorkOrderListItemDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.WorkOrderListSelect}
            {MaintenanceSql.WorkOrderListFrom}
            WHERE {where}
            ORDER BY wo.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<WorkOrderListItemDto>>.SuccessResponse(new PagedResult<WorkOrderListItemDto>
        {
            Items = rows.ToList(),
            TotalCount = count,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public class GetWorkOrderByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWorkOrderByIdQuery, ApiResponse<WorkOrderDetailDto>>
{
    public async Task<ApiResponse<WorkOrderDetailDto>> Handle(
        GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<WorkOrderDetailRow>(new CommandDefinition("""
            SELECT wo.Id, wo.WorkOrderNumber, wo.RequestId, wo.VehicleId, v.Name AS VehicleName,
                   v.RegistrationNumber AS VehicleRegistration,
                   r.DriverId, d.FullName AS DriverName,
                   COALESCE(wo.BranchId, v.BranchId) AS BranchId, b.Name AS BranchName,
                   wo.WorkshopId, w.Name AS WorkshopName, wo.TechnicianId, t.FullName AS TechnicianName,
                   wo.ServiceTypeId, wo.ServiceTypeName, wo.MaintenanceType,
                   wo.StartDate, wo.EstimatedCompletionDate, wo.CompletedAt,
                   wo.LaborCost, wo.PartsCost, wo.TotalCost,
                   wo.EstimatedLaborCost, wo.EstimatedPartsCost,
                   wo.Status, wo.Priority, wo.Notes, wo.TechnicianNotes, wo.CreatedAt
            FROM WorkOrders wo
            INNER JOIN Vehicles v ON v.Id = wo.VehicleId
            LEFT JOIN MaintenanceRequests r ON r.Id = wo.RequestId
            LEFT JOIN Drivers d ON d.Id = r.DriverId
            LEFT JOIN Branches b ON b.Id = COALESCE(wo.BranchId, v.BranchId)
            LEFT JOIN Workshops w ON w.Id = wo.WorkshopId
            LEFT JOIN Technicians t ON t.Id = wo.TechnicianId
            WHERE wo.Id = @Id AND wo.TenantId = @TenantId AND wo.IsDeleted = 0
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("WorkOrder", request.Id);

        var parts = await connection.QueryAsync<WorkOrderPartUsageDto>(new CommandDefinition("""
            SELECT pu.PartId, p.PartName, pu.Quantity, pu.UnitCost,
                   (pu.Quantity * pu.UnitCost) AS TotalCost
            FROM PartUsage pu
            INNER JOIN Parts p ON p.Id = pu.PartId AND p.IsDeleted = 0
            WHERE pu.WorkOrderId = @Id AND pu.TenantId = @TenantId
            ORDER BY pu.UsedAt DESC
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        var detail = new WorkOrderDetailDto(
            row.Id, row.WorkOrderNumber, row.RequestId, row.VehicleId, row.VehicleName,
            row.VehicleRegistration, row.DriverId, row.DriverName, row.BranchId, row.BranchName,
            row.WorkshopId, row.WorkshopName, row.TechnicianId, row.TechnicianName,
            row.ServiceTypeId, row.ServiceTypeName, row.MaintenanceType,
            row.StartDate, row.EstimatedCompletionDate, row.CompletedAt,
            row.LaborCost, row.PartsCost, row.TotalCost,
            row.EstimatedLaborCost, row.EstimatedPartsCost,
            row.Status, row.Priority, row.Notes, row.TechnicianNotes, row.CreatedAt,
            parts.ToList());
        return ApiResponse<WorkOrderDetailDto>.SuccessResponse(detail);
    }
}

public class ListTechniciansQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListTechniciansQuery, ApiResponse<IReadOnlyList<TechnicianListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TechnicianListItemDto>>> Handle(
        ListTechniciansQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var clauses = new List<string> { "t.IsDeleted = 0", "t.IsActive = 1", "t.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);

        if (request.WorkshopId.HasValue)
        {
            clauses.Add("t.WorkshopId = @WorkshopId");
            p.Add("WorkshopId", request.WorkshopId.Value);
        }

        var where = string.Join(" AND ", clauses);
        var rows = await connection.QueryAsync<TechnicianListItemDto>(new CommandDefinition($"""
            SELECT t.Id, t.FullName, t.WorkshopId, w.Name AS WorkshopName
            FROM Technicians t
            LEFT JOIN Workshops w ON w.Id = t.WorkshopId
            WHERE {where}
            ORDER BY t.FullName
            """, p, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<TechnicianListItemDto>>.SuccessResponse(rows.ToList());
    }
}
