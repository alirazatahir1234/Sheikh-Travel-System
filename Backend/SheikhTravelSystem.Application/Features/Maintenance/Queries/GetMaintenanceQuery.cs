using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;

namespace SheikhTravelSystem.Application.Features.Maintenance.Queries;

public record GetMaintenanceQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<MaintenanceDto>>>;

public class GetMaintenanceQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetMaintenanceQuery, ApiResponse<PagedResult<MaintenanceDto>>>
{
    public async Task<ApiResponse<PagedResult<MaintenanceDto>>> Handle(GetMaintenanceQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var records = await connection.QueryAsync<MaintenanceDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt
                  FROM Maintenance WHERE IsDeleted = 0
                  ORDER BY MaintenanceDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Maintenance WHERE IsDeleted = 0",
                cancellationToken: cancellationToken));

        var result = new PagedResult<MaintenanceDto>
        {
            Items = records.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<MaintenanceDto>>.SuccessResponse(result);
    }
}
