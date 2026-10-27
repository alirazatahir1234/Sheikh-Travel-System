using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetMyDriverPerformanceQuery(DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<DriverScoreDto>>;

public class GetMyDriverPerformanceQueryHandler(
    IMediator mediator,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyDriverPerformanceQuery, ApiResponse<DriverScoreDto>>
{
    public async Task<ApiResponse<DriverScoreDto>> Handle(
        GetMyDriverPerformanceQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverScoreDto>.FailResponse("Driver identity required.");

        var ranking = await mediator.Send(
            new GetDriverScoreRankingQuery(request.FromDate, request.ToDate, null, null),
            cancellationToken);

        if (!ranking.Success || ranking.Data is null)
            return ApiResponse<DriverScoreDto>.FailResponse(ranking.Message ?? "Failed to load performance.");

        var mine = ranking.Data.FirstOrDefault(r => r.DriverId == driverId.Value);
        if (mine is null)
        {
            return ApiResponse<DriverScoreDto>.SuccessResponse(new DriverScoreDto(
                driverId.Value,
                "You",
                0,
                "—",
                new DriverScoreFactorsDto(0, 0, 0, 0, 0, 0, null),
                IsPartial: true,
                Rank: 0,
                TotalDrivers: ranking.Data.Count));
        }

        return ApiResponse<DriverScoreDto>.SuccessResponse(mine);
    }
}
