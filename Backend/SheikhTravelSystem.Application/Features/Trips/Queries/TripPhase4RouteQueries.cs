using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Trips.DTOs;

namespace SheikhTravelSystem.Application.Features.Trips.Queries;

public record GetTripRouteSummaryQuery(int TripId) : IRequest<ApiResponse<TripRouteSummaryDto>>;

public class GetTripRouteSummaryQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetTripRouteSummaryQuery, ApiResponse<TripRouteSummaryDto>>
{
    public async Task<ApiResponse<TripRouteSummaryDto>> Handle(
        GetTripRouteSummaryQuery request, CancellationToken cancellationToken)
    {
        var dto = await tripRepository.GetRouteSummaryAsync(request.TripId, cancellationToken);
        return ApiResponse<TripRouteSummaryDto>.SuccessResponse(dto);
    }
}

public record OptimizeTripRouteCommand(int TripId) : IRequest<ApiResponse<bool>>;

public class OptimizeTripRouteCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<OptimizeTripRouteCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(OptimizeTripRouteCommand request, CancellationToken cancellationToken)
    {
        var result = await tripRepository.OptimizeRouteAsync(request.TripId, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage ?? "Unable to optimize route.");
        return ApiResponse<bool>.SuccessResponse(true, "Route optimized.");
    }
}
