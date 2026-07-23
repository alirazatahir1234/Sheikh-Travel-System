using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehiclesQuery(int Page = 1, int PageSize = 20, bool IncludeDrafts = false)
    : IRequest<ApiResponse<PagedResult<VehicleListItemDto>>>;

public class GetVehiclesQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine,
    IFileStorageService fileStorage)
    : IRequestHandler<GetVehiclesQuery, ApiResponse<PagedResult<VehicleListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleListItemDto>>> Handle(GetVehiclesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = tenantContext.GetRequiredTenantId();

        var clauses = new List<string> { "v.IsDeleted = 0", "v.TenantId = @TenantId" };
        if (!request.IncludeDrafts)
            clauses.Add("v.Status <> 5");

        var parameters = new DynamicParameters(new
        {
            Offset = offset,
            request.PageSize,
            TenantId = tenantId
        });

        if (currentUser.UserId is int userId)
        {
            var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            DataScopeSql.ApplyVehicleScope(parameters, scope, "v", clauses);
        }

        var whereClause = string.Join(" AND ", clauses);

        var vehicles = (await connection.QueryAsync<VehicleListItemDto>(
            new CommandDefinition(
                $@"SELECT {VehicleSql.ListSelect}
                  {VehicleSql.ListFrom}
                  WHERE {whereClause}
                  ORDER BY v.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken)))
            .Select(v => string.IsNullOrWhiteSpace(v.ImageUrl)
                ? v
                : v with { ImageUrl = fileStorage.ResolveReadUrl(v.ImageUrl) })
            .ToList();

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $@"SELECT COUNT(*) FROM Vehicles v WHERE {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        var result = new PagedResult<VehicleListItemDto>
        {
            Items = vehicles,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<VehicleListItemDto>>.SuccessResponse(result);
    }
}
