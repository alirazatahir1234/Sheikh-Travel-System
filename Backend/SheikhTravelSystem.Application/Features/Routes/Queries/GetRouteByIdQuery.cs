using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Queries;

public record GetRouteByIdQuery(int Id) : IRequest<ApiResponse<RouteDto>>;

public class GetRouteByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetRouteByIdQuery, ApiResponse<RouteDto>>
{
    public async Task<ApiResponse<RouteDto>> Handle(GetRouteByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var route = await connection.QuerySingleOrDefaultAsync<RouteDto>(
            new CommandDefinition(
                @"SELECT Id, Name, Source, Destination, Distance, EstimatedMinutes, BasePrice, IsActive, CreatedAt
                  FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (route is null)
            throw new NotFoundException("Route", request.Id);

        return ApiResponse<RouteDto>.SuccessResponse(route);
    }
}
