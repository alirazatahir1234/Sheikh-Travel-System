using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetGeofencesQuery(
    string? Search = null,
    string? AreaType = null,
    bool? IsActive = null,
    int? VehicleId = null) : IRequest<ApiResponse<List<GeofenceDto>>>;

public class GetGeofencesQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGeofencesQuery, ApiResponse<List<GeofenceDto>>>
{
    public Task<ApiResponse<List<GeofenceDto>>> Handle(GetGeofencesQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGeofencesAsync(request, cancellationToken);
}

public record GetGeofenceAssignmentsQuery(int GeofenceId) : IRequest<ApiResponse<List<GeofenceAssignmentDto>>>;

public class GetGeofenceAssignmentsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGeofenceAssignmentsQuery, ApiResponse<List<GeofenceAssignmentDto>>>
{
    public Task<ApiResponse<List<GeofenceAssignmentDto>>> Handle(GetGeofenceAssignmentsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGeofenceAssignmentsAsync(request, cancellationToken);
}

public record GetGeofenceStatsQuery : IRequest<ApiResponse<GeofenceStatsDto>>;

public class GetGeofenceStatsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGeofenceStatsQuery, ApiResponse<GeofenceStatsDto>>
{
    public Task<ApiResponse<GeofenceStatsDto>> Handle(GetGeofenceStatsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGeofenceStatsAsync(request, cancellationToken);
}

public record GetGeofenceEventsQuery(int GeofenceId, DateTime? From = null, DateTime? To = null)
    : IRequest<ApiResponse<List<GpsAlertEventDto>>>;

public class GetGeofenceEventsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGeofenceEventsQuery, ApiResponse<List<GpsAlertEventDto>>>
{
    public Task<ApiResponse<List<GpsAlertEventDto>>> Handle(GetGeofenceEventsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGeofenceEventsAsync(request, cancellationToken);
}
