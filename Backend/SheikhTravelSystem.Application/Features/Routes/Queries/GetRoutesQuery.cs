using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Queries;

public record GetRoutesQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<RouteDto>>>;

public class GetRoutesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetRoutesQuery, ApiResponse<PagedResult<RouteDto>>>
{
    public async Task<ApiResponse<PagedResult<RouteDto>>> Handle(GetRoutesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var routes = await connection.QueryAsync<RouteDto>(
            new CommandDefinition(
                @"SELECT Id, Source, Destination, Distance, BasePrice, IsActive, CreatedAt
                  FROM Routes WHERE IsDeleted = 0
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Routes WHERE IsDeleted = 0",
                cancellationToken: cancellationToken));

        var result = new PagedResult<RouteDto>
        {
            Items = routes.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<RouteDto>>.SuccessResponse(result);
    }
}
