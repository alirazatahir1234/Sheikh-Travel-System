using Dapper;
using MediatR;
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

public class ListMaintenanceRequestsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListMaintenanceRequestsQuery, ApiResponse<PagedResult<MaintenanceRequestDto>>>
{
    public async Task<ApiResponse<PagedResult<MaintenanceRequestDto>>> Handle(ListMaintenanceRequestsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();
        var offset = (Math.Max(1, request.Page) - 1) * request.PageSize;

        var clauses = new List<string> { "r.IsDeleted = 0", "r.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Offset", offset);
        p.Add("PageSize", request.PageSize);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            clauses.Add("r.Status = @Status");
            p.Add("Status", request.Status.Trim());
        }
        if (request.VehicleId.HasValue)
        {
            clauses.Add("r.VehicleId = @VehicleId");
            p.Add("VehicleId", request.VehicleId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            clauses.Add("(r.RequestNumber LIKE @Search OR r.Description LIKE @Search OR v.Name LIKE @Search)");
            p.Add("Search", $"%{request.Search.Trim()}%");
        }

        var where = string.Join(" AND ", clauses);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {MaintenanceSql.RequestListFrom} WHERE {where}", p, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<MaintenanceRequestDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.RequestListSelect}
            {MaintenanceSql.RequestListFrom}
            WHERE {where}
            ORDER BY r.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<MaintenanceRequestDto>>.SuccessResponse(new PagedResult<MaintenanceRequestDto>
        {
            Items = rows.ToList(),
            TotalCount = count,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public class GetMaintenanceRequestByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceRequestByIdQuery, ApiResponse<MaintenanceRequestDto>>
{
    public async Task<ApiResponse<MaintenanceRequestDto>> Handle(GetMaintenanceRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<MaintenanceRequestDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.RequestListSelect}
            {MaintenanceSql.RequestListFrom}
            WHERE r.Id = @Id AND r.TenantId = @TenantId AND r.IsDeleted = 0
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null)
            throw new Common.Exceptions.NotFoundException("MaintenanceRequest", request.Id);

        return ApiResponse<MaintenanceRequestDto>.SuccessResponse(row);
    }
}
