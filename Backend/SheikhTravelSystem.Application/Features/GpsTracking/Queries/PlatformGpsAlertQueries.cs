using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

/// <summary>Super Admin — GPS alert events across all tenants (optional company filter).</summary>
public record GetPlatformGpsAlertEventsQuery(
    int? TenantId,
    int? VehicleId,
    bool? UnacknowledgedOnly,
    DateTime? From = null,
    DateTime? To = null,
    int? DriverId = null,
    string? EventType = null,
    string? Severity = null,
    string? Status = null,
    string? ReadState = null,
    string? DatePreset = null,
    int? GeofenceId = null)
    : IRequest<ApiResponse<List<GpsAlertEventDto>>>;

public class GetPlatformGpsAlertEventsQueryHandler(
    IGpsTrackingRepository gpsTrackingRepository,
    IPlatformScope platformScope)
    : IRequestHandler<GetPlatformGpsAlertEventsQuery, ApiResponse<List<GpsAlertEventDto>>>
{
    public Task<ApiResponse<List<GpsAlertEventDto>>> Handle(
        GetPlatformGpsAlertEventsQuery request,
        CancellationToken cancellationToken)
    {
        if (!platformScope.HasPermission(PlatformPermissions.TenantsDataRead))
            return Task.FromResult(ApiResponse<List<GpsAlertEventDto>>.FailResponse("Platform operator access required."));

        return gpsTrackingRepository.GetPlatformGpsAlertEventsAsync(request, cancellationToken);
    }
}

public record GetPlatformGpsAlertStatsQuery(int? TenantId = null) : IRequest<ApiResponse<GpsAlertStatsDto>>;

public class GetPlatformGpsAlertStatsQueryHandler(
    IGpsTrackingRepository gpsTrackingRepository,
    IPlatformScope platformScope)
    : IRequestHandler<GetPlatformGpsAlertStatsQuery, ApiResponse<GpsAlertStatsDto>>
{
    public Task<ApiResponse<GpsAlertStatsDto>> Handle(
        GetPlatformGpsAlertStatsQuery request,
        CancellationToken cancellationToken)
    {
        if (!platformScope.HasPermission(PlatformPermissions.TenantsDataRead))
            return Task.FromResult(ApiResponse<GpsAlertStatsDto>.FailResponse("Platform operator access required."));

        return gpsTrackingRepository.GetPlatformGpsAlertStatsAsync(request, cancellationToken);
    }
}
