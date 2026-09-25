using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetLivePositionsQuery(int Page = 1, int PageSize = 500) : IRequest<ApiResponse<PagedResult<PositionDto>>>;

/// <summary>
/// Full live-map fleet roster (GPS.View). Prefer this over composing /vehicles + /gps/live —
/// branch-scoped fleet managers and roles without Vehicle.View still get a consistent list.
/// </summary>
public record GetGpsLiveFleetQuery : IRequest<ApiResponse<List<GpsLiveFleetVehicleDto>>>;

public class GetGpsLiveFleetQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsLiveFleetQuery, ApiResponse<List<GpsLiveFleetVehicleDto>>>
{
    public Task<ApiResponse<List<GpsLiveFleetVehicleDto>>> Handle(GetGpsLiveFleetQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsLiveFleetAsync(request, cancellationToken);
}

public class GetLivePositionsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetLivePositionsQuery, ApiResponse<PagedResult<PositionDto>>>
{
    public Task<ApiResponse<PagedResult<PositionDto>>> Handle(GetLivePositionsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetLivePositionsAsync(request, cancellationToken);
}

public record GetPositionHistoryQuery(int VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<PositionDto>>>;

public class GetPositionHistoryQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetPositionHistoryQuery, ApiResponse<List<PositionDto>>>
{
    public Task<ApiResponse<List<PositionDto>>> Handle(GetPositionHistoryQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetPositionHistoryAsync(request, cancellationToken);
}

public record GetGpsTripsQuery(
    int? VehicleId,
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null,
    int Page = 1,
    int PageSize = 100,
    bool Unpaged = false,
    string? Search = null,
    string? SortBy = null,
    string? SortDir = null,
    double? MinDistanceKm = null,
    double? MaxDistanceKm = null,
    decimal? MinAvgSpeedKmh = null,
    decimal? MaxAvgSpeedKmh = null,
    string? Status = null)
    : IRequest<ApiResponse<PagedResult<GpsTripDto>>>;

public class GetGpsTripsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsTripsQuery, ApiResponse<PagedResult<GpsTripDto>>>
{
    public Task<ApiResponse<PagedResult<GpsTripDto>>> Handle(GetGpsTripsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsTripsAsync(request, cancellationToken);
}

public record GetGpsAlertRulesQuery : IRequest<ApiResponse<List<GpsAlertRuleDto>>>;

public class GetGpsAlertRulesQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsAlertRulesQuery, ApiResponse<List<GpsAlertRuleDto>>>
{
    public Task<ApiResponse<List<GpsAlertRuleDto>>> Handle(GetGpsAlertRulesQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsAlertRulesAsync(request, cancellationToken);
}

public record GetGpsAlertEventsQuery(
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

public class GetGpsAlertEventsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsAlertEventsQuery, ApiResponse<List<GpsAlertEventDto>>>
{
    public Task<ApiResponse<List<GpsAlertEventDto>>> Handle(GetGpsAlertEventsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsAlertEventsAsync(request, cancellationToken);
}

public record GetGpsAlertEventByIdQuery(int Id) : IRequest<ApiResponse<GpsAlertEventDto>>;

public class GetGpsAlertEventByIdQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsAlertEventByIdQuery, ApiResponse<GpsAlertEventDto>>
{
    public Task<ApiResponse<GpsAlertEventDto>> Handle(GetGpsAlertEventByIdQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsAlertEventByIdAsync(request, cancellationToken);
}

public record GetGpsAlertStatsQuery : IRequest<ApiResponse<GpsAlertStatsDto>>;

public class GetGpsAlertStatsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGpsAlertStatsQuery, ApiResponse<GpsAlertStatsDto>>
{
    public Task<ApiResponse<GpsAlertStatsDto>> Handle(GetGpsAlertStatsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGpsAlertStatsAsync(request, cancellationToken);
}

public static class GpsAlertQueryProjection
{
    public static IEnumerable<GpsAlertEventDto> Decorate(IEnumerable<GpsAlertEventDto> rows, ICurrentUserService currentUser) =>
        rows.Select(row => Decorate(row, currentUser));

    public static GpsAlertEventDto Decorate(GpsAlertEventDto row, ICurrentUserService currentUser)
    {
        row.CanAcknowledge = row.Status != "archived" && row.Status != "resolved" && GpsAlertAccess.CanAcknowledge(currentUser);
        row.CanResolve = row.Status != "resolved" && row.Status != "archived" && GpsAlertAccess.CanResolve(currentUser);
        row.CanArchive = row.Status is "acknowledged" or "resolved" && GpsAlertAccess.CanArchive(currentUser);
        row.CanDelete = GpsAlertAccess.CanDelete(currentUser);
        return row;
    }
}

public record GetAlertSettingsQuery : IRequest<ApiResponse<List<AlertSettingDto>>>;

public class GetAlertSettingsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetAlertSettingsQuery, ApiResponse<List<AlertSettingDto>>>
{
    public Task<ApiResponse<List<AlertSettingDto>>> Handle(GetAlertSettingsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetAlertSettingsAsync(request, cancellationToken);
}

public record GetGpsDevicesQuery : IRequest<ApiResponse<List<GpsDeviceDto>>>;

public class GetGpsDevicesQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetGpsDevicesQuery, ApiResponse<List<GpsDeviceDto>>>
{
    public Task<ApiResponse<List<GpsDeviceDto>>> Handle(GetGpsDevicesQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetGpsDevicesAsync(request, cancellationToken);
}

public record GetDeviceCommandsQuery(
    int GpsDeviceId,
    string? Status = null,
    string? CommandType = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50)
    : IRequest<ApiResponse<List<GpsDeviceCommandDto>>>;

public class GetDeviceCommandsQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetDeviceCommandsQuery, ApiResponse<List<GpsDeviceCommandDto>>>
{
    public Task<ApiResponse<List<GpsDeviceCommandDto>>> Handle(GetDeviceCommandsQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetDeviceCommandsAsync(request, cancellationToken);
}

public record GetDeviceCommandByIdQuery(int Id) : IRequest<ApiResponse<GpsDeviceCommandDetailDto>>;

public class GetDeviceCommandByIdQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetDeviceCommandByIdQuery, ApiResponse<GpsDeviceCommandDetailDto>>
{
    public Task<ApiResponse<GpsDeviceCommandDetailDto>> Handle(GetDeviceCommandByIdQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetDeviceCommandByIdAsync(request, cancellationToken);
}

public record GetVehicleCommandsQuery(int VehicleId, int Page = 1, int PageSize = 50)
    : IRequest<ApiResponse<List<GpsDeviceCommandDto>>>;

public class GetVehicleCommandsQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetVehicleCommandsQuery, ApiResponse<List<GpsDeviceCommandDto>>>
{
    public Task<ApiResponse<List<GpsDeviceCommandDto>>> Handle(GetVehicleCommandsQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetVehicleCommandsAsync(request, cancellationToken);
}

public record GetDeviceSupportedCommandsQuery(int GpsDeviceId) : IRequest<ApiResponse<List<SupportedCommandDto>>>;

public class GetDeviceSupportedCommandsQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetDeviceSupportedCommandsQuery, ApiResponse<List<SupportedCommandDto>>>
{
    public Task<ApiResponse<List<SupportedCommandDto>>> Handle(GetDeviceSupportedCommandsQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetDeviceSupportedCommandsAsync(request, cancellationToken);
}

public record GetPendingDeviceCommandsQuery(string UniqueId) : IRequest<ApiResponse<List<GpsDeviceCommandDto>>>;

public class GetPendingDeviceCommandsQueryHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<GetPendingDeviceCommandsQuery, ApiResponse<List<GpsDeviceCommandDto>>>
{
    public Task<ApiResponse<List<GpsDeviceCommandDto>>> Handle(GetPendingDeviceCommandsQuery request, CancellationToken cancellationToken)
        => gpsDeviceRepository.GetPendingDeviceCommandsAsync(request, cancellationToken);
}

public record GetGpsEtaQuery(int BookingId) : IRequest<ApiResponse<GpsEtaDto>>;

public class GetGpsEtaQueryHandler(
    IGpsTrackingRepository gpsTrackingRepository,
    IGoogleRoutesService routesService)
    : IRequestHandler<GetGpsEtaQuery, ApiResponse<GpsEtaDto>>
{
    public async Task<ApiResponse<GpsEtaDto>> Handle(
        GetGpsEtaQuery request, CancellationToken cancellationToken)
    {
        var result = await gpsTrackingRepository.GetGpsEtaAsync(request, cancellationToken);
        if (!result.Success || result.Data is null)
            return result;

        var route = await routesService.ComputeRouteAsync(
            result.Data.DriverLatitude,
            result.Data.DriverLongitude,
            result.Data.PickupLatitude,
            result.Data.PickupLongitude,
            null,
            cancellationToken);

        if (route is null || route.DistanceMeters <= 0)
            return result;

        var enhanced = result.Data with
        {
            DistanceKm = Math.Round(route.DistanceMeters / 1000.0, 2),
            EtaMinutes = route.DurationSeconds > 0
                ? (int)Math.Ceiling(route.DurationSeconds / 60.0)
                : result.Data.EtaMinutes
        };

        return ApiResponse<GpsEtaDto>.SuccessResponse(enhanced);
    }
}

public record GetGeofenceBreachCountQuery : IRequest<ApiResponse<int>>;

public class GetGeofenceBreachCountQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetGeofenceBreachCountQuery, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(GetGeofenceBreachCountQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetGeofenceBreachCountAsync(request, cancellationToken);
}
