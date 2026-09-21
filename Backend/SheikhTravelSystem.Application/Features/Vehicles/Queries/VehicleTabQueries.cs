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

public record GetVehicleMaintenanceQuery(int VehicleId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<VehicleMaintenanceDto>>>;

public class GetVehicleMaintenanceQueryHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleMaintenanceQuery, ApiResponse<PagedResult<VehicleMaintenanceDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleMaintenanceDto>>> Handle(
        GetVehicleMaintenanceQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await vehicleRepository.GetMaintenancePagedAsync(
            request.VehicleId, tenantContext.GetRequiredTenantId(),
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

public record GetVehicleFuelQuery(int VehicleId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<VehicleFuelSummaryDto>>;

public class GetVehicleFuelQueryHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleFuelQuery, ApiResponse<VehicleFuelSummaryDto>>
{
    public async Task<ApiResponse<VehicleFuelSummaryDto>> Handle(
        GetVehicleFuelQuery request, CancellationToken cancellationToken)
    {
        var result = await vehicleRepository.GetFuelSummaryAsync(
            request.VehicleId, tenantContext.GetRequiredTenantId(),
            request.Page, request.PageSize, cancellationToken);
        return ApiResponse<VehicleFuelSummaryDto>.SuccessResponse(result);
    }
}

public record GetVehicleGpsQuery(int VehicleId) : IRequest<ApiResponse<VehicleGpsDto>>;

public class GetVehicleGpsQueryHandler(
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<GetVehicleGpsQuery, ApiResponse<VehicleGpsDto>>
{
    public async Task<ApiResponse<VehicleGpsDto>> Handle(
        GetVehicleGpsQuery request, CancellationToken cancellationToken)
    {
        var row = await vehicleRepository.GetGpsSnapshotAsync(
            request.VehicleId, tenantContext.GetRequiredTenantId(), cancellationToken);

        if (row is null)
            throw new NotFoundException("Vehicle", request.VehicleId);

        row = await OverlayLiveTraccarAsync(row, cancellationToken);

        var lastUpdate = GpsUtcDateTime.AsUtc(row.LastUpdate);
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

            if (row.LastUpdate is { } localTs)
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

            return row with
            {
                LastSeenAt = fixTime,
                LastIgnition = pos.Attributes?.Ignition ?? row.LastIgnition,
                Latitude = pos.Latitude,
                Longitude = pos.Longitude,
                Speed = speedKmh,
                LastUpdate = fixTime,
                TotalDistanceKm = totalDistanceKm,
                BatteryLevel = pos.Attributes?.BatteryLevel ?? row.BatteryLevel,
                GsmSignal = pos.Attributes?.Rssi ?? row.GsmSignal,
                Address = address,
                GpsOnline = true,
                Heading = (decimal)pos.Course,
                FuelLevel = pos.Attributes?.Fuel ?? row.FuelLevel
            };
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
