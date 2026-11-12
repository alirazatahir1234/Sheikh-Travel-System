using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using VehicleFuelSummaryDto = SheikhTravelSystem.Application.Features.Vehicles.DTOs.VehicleFuelSummaryDto;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehicleMaintenanceQuery(int VehicleId, int Page = 1, int PageSize = 20, int? TenantId = null)
    : IRequest<ApiResponse<PagedResult<VehicleMaintenanceDto>>>;

public class GetVehicleMaintenanceQueryHandler(IVehicleRepository vehicleRepository, IPlatformScope platformScope)
    : IRequestHandler<GetVehicleMaintenanceQuery, ApiResponse<PagedResult<VehicleMaintenanceDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleMaintenanceDto>>> Handle(
        GetVehicleMaintenanceQuery request, CancellationToken cancellationToken)
    {
        var tenantId = platformScope.ResolveTenantId(request.TenantId);
        var (items, totalCount) = await vehicleRepository.GetMaintenancePagedAsync(
            request.VehicleId, tenantId,
            request.Page, request.PageSize, cancellationToken);

        return ApiResponse<PagedResult<VehicleMaintenanceDto>>.SuccessResponse(new PagedResult<VehicleMaintenanceDto>
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public record GetVehicleFuelQuery(int VehicleId, int Page = 1, int PageSize = 20, int? TenantId = null)
    : IRequest<ApiResponse<VehicleFuelSummaryDto>>;

public class GetVehicleFuelQueryHandler(IVehicleRepository vehicleRepository, IPlatformScope platformScope)
    : IRequestHandler<GetVehicleFuelQuery, ApiResponse<VehicleFuelSummaryDto>>
{
    public async Task<ApiResponse<VehicleFuelSummaryDto>> Handle(
        GetVehicleFuelQuery request, CancellationToken cancellationToken)
    {
        var tenantId = platformScope.ResolveTenantId(request.TenantId);
        var result = await vehicleRepository.GetFuelSummaryAsync(
            request.VehicleId, tenantId,
            request.Page, request.PageSize, cancellationToken);
        return ApiResponse<VehicleFuelSummaryDto>.SuccessResponse(result);
    }
}

public record GetVehicleGpsQuery(int VehicleId, int? TenantId = null) : IRequest<ApiResponse<VehicleGpsDto>>;

public class GetVehicleGpsQueryHandler(
    IVehicleRepository vehicleRepository,
    IPlatformScope platformScope,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<GetVehicleGpsQuery, ApiResponse<VehicleGpsDto>>
{
    public async Task<ApiResponse<VehicleGpsDto>> Handle(
        GetVehicleGpsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = platformScope.ResolveTenantId(request.TenantId);
        var row = await vehicleRepository.GetGpsSnapshotAsync(
            request.VehicleId, tenantId, cancellationToken);

        if (row is null)
            throw new NotFoundException("Vehicle", request.VehicleId);

        row = await OverlayLiveTraccarAsync(row, cancellationToken);

        var lastUpdate = GpsUtcDateTime.AsUtc(row.PositionUpdatedAt);
        var lastSeen = GpsUtcDateTime.AsUtc(row.LastSeenAt);
        var online = IsOnline(lastUpdate, lastSeen);

        var dto = new VehicleGpsDto(
            row.GpsDeviceId, row.DeviceName, row.UniqueId, row.IsActive, lastSeen, row.LastIgnition,
            row.Latitude, row.Longitude, row.Speed, lastUpdate, row.SimNumber, row.ModelName, row.BrandName,
            GpsUtcDateTime.AsUtc(row.InstallationDate), row.TotalDistanceKm, row.BatteryLevel, row.GsmSignal,
            row.Address, online, row.Heading, row.FuelLevel);

        return ApiResponse<VehicleGpsDto>.SuccessResponse(dto);
    }

    private static bool IsOnline(DateTime? lastUpdate, DateTime? lastSeen)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        return (lastUpdate.HasValue && lastUpdate.Value > cutoff)
            || (lastSeen.HasValue && lastSeen.Value > cutoff);
    }

    private async Task<VehicleGpsSnapshot> OverlayLiveTraccarAsync(
        VehicleGpsSnapshot row, CancellationToken cancellationToken)
    {
        var opts = traccarOptions.Value;
        if (!opts.Enabled || !opts.IsConfigured || row.TraccarDeviceId is null or <= 0)
            return row;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(2500));

            var pos = await traccarClient.GetLatestPositionByDeviceAsync(
                row.TraccarDeviceId.Value, timeoutCts.Token);
            if (pos is null || (!pos.Valid && pos.Latitude == 0 && pos.Longitude == 0))
                return row;

            var fixTime = pos.FixTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(pos.FixTime, DateTimeKind.Utc)
                : pos.FixTime.ToUniversalTime();

            if (row.PositionUpdatedAt is { } localTs)
            {
                var localUtc = localTs.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(localTs, DateTimeKind.Utc)
                    : localTs.ToUniversalTime();
                if (fixTime < localUtc.AddSeconds(-5))
                    return row;
            }

            var speedKmh = (decimal)(pos.Speed * 1.852);
            var totalDistanceKm = pos.Attributes?.TotalDistance is { } meters
                ? meters / 1000m
                : row.TotalDistanceKm;
            var address = !string.IsNullOrWhiteSpace(pos.Address) ? pos.Address : row.Address;

            // Mutate in place — VehicleGpsSnapshot is a Dapper-friendly class, not a record.
            row.LastSeenAt = fixTime;
            row.LastIgnition = pos.Attributes?.Ignition ?? row.LastIgnition;
            row.Latitude = pos.Latitude;
            row.Longitude = pos.Longitude;
            row.Speed = speedKmh;
            row.PositionUpdatedAt = fixTime;
            row.TotalDistanceKm = totalDistanceKm;
            row.BatteryLevel = pos.Attributes?.BatteryLevel ?? row.BatteryLevel;
            row.GsmSignal = pos.Attributes?.Rssi ?? row.GsmSignal;
            row.Address = address;
            row.GpsOnline = true;
            row.Heading = (decimal)pos.Course;
            row.FuelLevel = pos.Attributes?.Fuel ?? row.FuelLevel;
            return row;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return row;
        }
        catch
        {
            return row;
        }
    }
}
