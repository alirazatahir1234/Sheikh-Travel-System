using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetGeofenceAnalyticsQuery(DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<GeofenceAnalyticsDto>>;

/// <summary>
/// Extends the (now tenant-fixed) GetGeofenceStatsQuery with a most/least-visited ranking and dwell
/// time — dwell is derived by pairing consecutive geofence_enter → geofence_exit events per
/// vehicle+geofence in the range, not a stored duration.
/// </summary>
public class GetGeofenceAnalyticsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGeofenceAnalyticsQuery, ApiResponse<GeofenceAnalyticsDto>>
{
    public Task<ApiResponse<GeofenceAnalyticsDto>> Handle(GetGeofenceAnalyticsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGeofenceAnalyticsAsync(request, cancellationToken);
}

public record GetAlertEventStatsQuery(DateTime? FromDate, DateTime? ToDate, int? VehicleId = null)
    : IRequest<ApiResponse<AlertEventStatsDto>>;

/// <summary>Thin wrapper — same filter/tenant-scoping shape as the (now tenant-fixed) GetGpsAlertEventsQuery, plus a GROUP BY through GpsEventTypeNormalizer.</summary>
public class GetAlertEventStatsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetAlertEventStatsQuery, ApiResponse<AlertEventStatsDto>>
{
    public Task<ApiResponse<AlertEventStatsDto>> Handle(GetAlertEventStatsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetAlertEventStatsAsync(request, cancellationToken);
}
