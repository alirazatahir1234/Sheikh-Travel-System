using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Queries;

public record GetRouteByIdQuery(int Id) : IRequest<ApiResponse<RouteDto>>;

public class GetRouteByIdQueryHandler(IRouteRepository routeRepository)
    : IRequestHandler<GetRouteByIdQuery, ApiResponse<RouteDto>>
{
    public async Task<ApiResponse<RouteDto>> Handle(GetRouteByIdQuery request, CancellationToken cancellationToken)
    {
        var route = await routeRepository.GetByIdAsync(request.Id, cancellationToken);
        return ApiResponse<RouteDto>.SuccessResponse(route);
    }
}
