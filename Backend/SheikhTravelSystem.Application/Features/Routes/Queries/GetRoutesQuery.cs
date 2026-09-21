using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Queries;

public record GetRoutesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null,
    string? DistanceBand = null,
    string? PriceBand = null
) : IRequest<ApiResponse<PagedResult<RouteDto>>>;

public class GetRoutesQueryHandler(IRouteRepository routeRepository)
    : IRequestHandler<GetRoutesQuery, ApiResponse<PagedResult<RouteDto>>>
{
    public async Task<ApiResponse<PagedResult<RouteDto>>> Handle(GetRoutesQuery request, CancellationToken cancellationToken)
    {
        var paged = await routeRepository.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.IsActive,
            request.DistanceBand,
            request.PriceBand,
            cancellationToken);

        var result = new PagedResult<RouteDto>
        {
            Items = paged.Items.ToList(),
            TotalCount = paged.TotalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<RouteDto>>.SuccessResponse(result);
    }
}
