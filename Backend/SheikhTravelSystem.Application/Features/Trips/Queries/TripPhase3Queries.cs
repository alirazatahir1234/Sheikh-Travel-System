using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Trips.DTOs;

namespace SheikhTravelSystem.Application.Features.Trips.Queries;

public record GetTripCalendarQuery(DateTime From, DateTime To)
    : IRequest<ApiResponse<IReadOnlyList<TripCalendarItemDto>>>;

public class GetTripCalendarQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetTripCalendarQuery, ApiResponse<IReadOnlyList<TripCalendarItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TripCalendarItemDto>>> Handle(
        GetTripCalendarQuery request, CancellationToken cancellationToken)
    {
        var rows = await tripRepository.GetCalendarAsync(request.From, request.To, cancellationToken);
        return ApiResponse<IReadOnlyList<TripCalendarItemDto>>.SuccessResponse(rows);
    }
}

public record GetLiveTripsQuery(bool TodayOnly = true)
    : IRequest<ApiResponse<IReadOnlyList<TripListItemDto>>>;

public class GetLiveTripsQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetLiveTripsQuery, ApiResponse<IReadOnlyList<TripListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TripListItemDto>>> Handle(
        GetLiveTripsQuery request, CancellationToken cancellationToken)
    {
        var rows = await tripRepository.GetLiveAsync(request.TodayOnly, cancellationToken);
        return ApiResponse<IReadOnlyList<TripListItemDto>>.SuccessResponse(rows);
    }
}

public record GetTripAnalyticsQuery(DateTime? From = null, DateTime? To = null)
    : IRequest<ApiResponse<TripAnalyticsDto>>;

public class GetTripAnalyticsQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetTripAnalyticsQuery, ApiResponse<TripAnalyticsDto>>
{
    public async Task<ApiResponse<TripAnalyticsDto>> Handle(
        GetTripAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var dto = await tripRepository.GetAnalyticsAsync(request.From, request.To, cancellationToken);
        return ApiResponse<TripAnalyticsDto>.SuccessResponse(dto);
    }
}
