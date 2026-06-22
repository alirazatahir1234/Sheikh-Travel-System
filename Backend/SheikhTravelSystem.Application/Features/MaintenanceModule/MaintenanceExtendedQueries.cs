using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

// ── Phase 4: History & Reports ────────────────────────────────────────────────

public record GetWorkshopVendorStatsQuery() : IRequest<ApiResponse<WorkshopVendorStatsDto>>;

public class GetWorkshopVendorStatsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWorkshopVendorStatsQuery, ApiResponse<WorkshopVendorStatsDto>>
{
    public async Task<ApiResponse<WorkshopVendorStatsDto>> Handle(
        GetWorkshopVendorStatsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var stats = await connection.QuerySingleAsync<WorkshopVendorStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM Workshops WHERE TenantId = @TenantId AND IsDeleted = 0) AS TotalWorkshops,
                (SELECT COUNT(*) FROM Workshops WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1) AS ActiveWorkshops,
                (SELECT COUNT(*) FROM Vendors WHERE TenantId = @TenantId AND IsDeleted = 0) AS TotalVendors,
                (SELECT COUNT(*) FROM Vendors WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND IsPreferred = 1) AS PreferredVendors
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WorkshopVendorStatsDto>.SuccessResponse(stats);
    }
}

public record GetMaintenanceHistoryQuery(
    int? VehicleId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? ServiceType = null,
    decimal? MinCost = null,
    decimal? MaxCost = null)
    : IRequest<ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>>;

public class GetMaintenanceHistoryQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceHistoryQuery, ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>> Handle(
        GetMaintenanceHistoryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var clauses = new List<string>();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);

        if (request.VehicleId.HasValue) { clauses.Add("h.VehicleId = @VehicleId"); p.Add("VehicleId", request.VehicleId); }
        if (request.From.HasValue) { clauses.Add("h.ServiceDate >= @From"); p.Add("From", request.From); }
        if (request.To.HasValue) { clauses.Add("h.ServiceDate < @To"); p.Add("To", request.To); }
        if (!string.IsNullOrWhiteSpace(request.ServiceType))
        {
            clauses.Add("h.ServiceType LIKE @ServiceType");
            p.Add("ServiceType", $"%{request.ServiceType.Trim()}%");
        }
        if (request.MinCost.HasValue) { clauses.Add("h.TotalCost >= @MinCost"); p.Add("MinCost", request.MinCost); }
        if (request.MaxCost.HasValue) { clauses.Add("h.TotalCost <= @MaxCost"); p.Add("MaxCost", request.MaxCost); }

        var where = clauses.Count > 0 ? $"WHERE {string.Join(" AND ", clauses)}" : "";
        var rows = await connection.QueryAsync<ServiceHistoryRow>(new CommandDefinition($"""
            SELECT {MaintenanceSql.ServiceHistorySelect}
            FROM (
                {MaintenanceSql.ServiceHistoryUnion}
            ) h
            {where}
            ORDER BY h.ServiceDate DESC
            """, p, cancellationToken: cancellationToken));

        var items = rows.Select(MaintenanceServiceHistoryHelper.MapRow).ToList();
        return ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>.SuccessResponse(items);
    }
}
