using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Queries;

public record GetRouteListStatsQuery(
    string? Search = null,
    bool? IsActive = null,
    string? PriceBand = null
) : IRequest<ApiResponse<RouteListStatsDto>>;

public class GetRouteListStatsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetRouteListStatsQuery, ApiResponse<RouteListStatsDto>>
{
    public async Task<ApiResponse<RouteListStatsDto>> Handle(GetRouteListStatsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var (whereClause, parameters) = RouteQueryFilters.Build(
            request.Search,
            request.IsActive,
            distanceBand: null,
            request.PriceBand,
            applyDistanceBand: false);

        parameters.Add("ShortKmMax", RouteQueryFilters.ShortKmMax);
        parameters.Add("MediumKmMax", RouteQueryFilters.MediumKmMax);

        var stats = await connection.QuerySingleAsync<RouteListStatsDto>(
            new CommandDefinition(
                $@"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Distance > 0 AND Distance < @ShortKmMax THEN 1 ELSE 0 END) AS Short,
                    SUM(CASE WHEN Distance >= @ShortKmMax AND Distance <= @MediumKmMax THEN 1 ELSE 0 END) AS Medium,
                    SUM(CASE WHEN Distance > @MediumKmMax THEN 1 ELSE 0 END) AS Long
                  FROM Routes
                  {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return ApiResponse<RouteListStatsDto>.SuccessResponse(stats);
    }
}
