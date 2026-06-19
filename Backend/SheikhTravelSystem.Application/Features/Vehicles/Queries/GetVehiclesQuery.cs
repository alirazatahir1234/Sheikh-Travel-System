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
    IFileStorageService fileStorage)
    : IRequestHandler<GetVehiclesQuery, ApiResponse<PagedResult<VehicleListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleListItemDto>>> Handle(GetVehiclesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = tenantContext.GetRequiredTenantId();

        var draftFilter = request.IncludeDrafts ? "" : " AND v.Status <> 5";

        var vehicles = (await connection.QueryAsync<VehicleListItemDto>(
            new CommandDefinition(
                $@"SELECT {VehicleSql.ListSelect}
                  {VehicleSql.ListFrom}
                  WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId{draftFilter}
                  ORDER BY v.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, request.PageSize, TenantId = tenantId },
                cancellationToken: cancellationToken)))
            .Select(v => string.IsNullOrWhiteSpace(v.ImageUrl)
                ? v
                : v with { ImageUrl = fileStorage.ResolveReadUrl(v.ImageUrl) })
            .ToList();

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId{draftFilter.Replace("v.", "")}",
                new { TenantId = tenantId },
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
