using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Application.Features.Tracking.Queries;

public record GetLiveTrackingQuery : IRequest<ApiResponse<List<TrackingDto>>>;

public class GetLiveTrackingQueryHandler(ITrackingRepository trackingRepository)
    : IRequestHandler<GetLiveTrackingQuery, ApiResponse<List<TrackingDto>>>
{
    public async Task<ApiResponse<List<TrackingDto>>> Handle(
        GetLiveTrackingQuery request,
        CancellationToken cancellationToken)
    {
        var tracking = await trackingRepository.GetLiveAsync(cancellationToken);
        return ApiResponse<List<TrackingDto>>.SuccessResponse(tracking.ToList());
    }
}

public record GetTrackingHistoryQuery(int VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<TrackingDto>>>;

public class GetTrackingHistoryQueryHandler(ITrackingRepository trackingRepository)
    : IRequestHandler<GetTrackingHistoryQuery, ApiResponse<List<TrackingDto>>>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(30);

    public async Task<ApiResponse<List<TrackingDto>>> Handle(
        GetTrackingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
            return ApiResponse<List<TrackingDto>>.FailResponse("'from' must be before 'to'.");

        if (toDate - fromDate > MaxRange)
            return ApiResponse<List<TrackingDto>>.FailResponse("Date range cannot exceed 30 days.");

        var history = await trackingRepository.GetHistoryAsync(
            request.VehicleId, fromDate, toDate, cancellationToken);

        return ApiResponse<List<TrackingDto>>.SuccessResponse(history.ToList());
    }
}
