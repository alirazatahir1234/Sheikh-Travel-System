using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Queries;

public record GetRouteListStatsQuery(
    string? Search = null,
    bool? IsActive = null,
    string? PriceBand = null
) : IRequest<ApiResponse<RouteListStatsDto>>;

public class GetRouteListStatsQueryHandler(IRouteRepository routeRepository)
    : IRequestHandler<GetRouteListStatsQuery, ApiResponse<RouteListStatsDto>>
{
    public async Task<ApiResponse<RouteListStatsDto>> Handle(GetRouteListStatsQuery request, CancellationToken cancellationToken)
    {
        var stats = await routeRepository.GetListStatsAsync(
            request.Search,
            request.IsActive,
            request.PriceBand,
            cancellationToken);

        return ApiResponse<RouteListStatsDto>.SuccessResponse(stats);
    }
}
