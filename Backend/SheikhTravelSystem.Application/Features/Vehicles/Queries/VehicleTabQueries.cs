using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using VehicleFuelDto = SheikhTravelSystem.Application.Features.Vehicles.DTOs.VehicleFuelDto;
using VehicleFuelSummaryDto = SheikhTravelSystem.Application.Features.Vehicles.DTOs.VehicleFuelSummaryDto;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehicleMaintenanceQuery(int VehicleId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<VehicleMaintenanceDto>>>;

public class GetVehicleMaintenanceQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleMaintenanceQuery, ApiResponse<PagedResult<VehicleMaintenanceDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleMaintenanceDto>>> Handle(
        GetVehicleMaintenanceQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicleExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleExists)
            throw new NotFoundException("Vehicle", request.VehicleId);

        var offset = (request.Page - 1) * request.PageSize;

        var records = await connection.QueryAsync<VehicleMaintenanceDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt
                  FROM Maintenance
                  WHERE IsDeleted = 0 AND VehicleId = @VehicleId
                  ORDER BY MaintenanceDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { request.VehicleId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Maintenance WHERE IsDeleted = 0 AND VehicleId = @VehicleId",
                new { request.VehicleId },
                cancellationToken: cancellationToken));

        var result = new PagedResult<VehicleMaintenanceDto>
        {
            Items = records.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<VehicleMaintenanceDto>>.SuccessResponse(result);
    }
}

public record GetVehicleFuelQuery(int VehicleId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<VehicleFuelSummaryDto>>;

public class GetVehicleFuelQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleFuelQuery, ApiResponse<VehicleFuelSummaryDto>>
{
    public async Task<ApiResponse<VehicleFuelSummaryDto>> Handle(
        GetVehicleFuelQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicleExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleExists)
            throw new NotFoundException("Vehicle", request.VehicleId);

        var offset = (request.Page - 1) * request.PageSize;

        var logs = await connection.QueryAsync<VehicleFuelDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, CreatedAt
                  FROM FuelLogs
                  WHERE IsDeleted = 0 AND VehicleId = @VehicleId
                  ORDER BY FuelDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { request.VehicleId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totals = await connection.QuerySingleAsync<(decimal TotalLiters, decimal TotalCost, int TotalCount)>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Liters), 0) AS TotalLiters,
                         ISNULL(SUM(TotalCost), 0) AS TotalCost,
                         COUNT(*) AS TotalCount
                  FROM FuelLogs WHERE IsDeleted = 0 AND VehicleId = @VehicleId",
                new { request.VehicleId },
                cancellationToken: cancellationToken));

        var result = new VehicleFuelSummaryDto(logs.ToList(), totals.TotalLiters, totals.TotalCost, totals.TotalCount);
        return ApiResponse<VehicleFuelSummaryDto>.SuccessResponse(result);
    }
}

public record GetVehicleGpsQuery(int VehicleId) : IRequest<ApiResponse<VehicleGpsDto>>;

/// <summary>
/// Vehicle profile / drawer GPS card. Reads local cache first, then overlays a live Traccar
/// position (same source as the Live Map) so the profile never lags the map by hours.
/// </summary>
public class GetVehicleGpsQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<GetVehicleGpsQuery, ApiResponse<VehicleGpsDto>>
{
    private sealed class VehicleGpsRow
    {
        public int? GpsDeviceId { get; init; }
        public string? DeviceName { get; init; }
        public string? UniqueId { get; init; }
        public bool? IsActive { get; init; }
        public DateTime? LastSeenAt { get; init; }
        public bool? LastIgnition { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public decimal? Speed { get; init; }
        public DateTime? LastUpdate { get; init; }
        public string? SimNumber { get; init; }
        public string? ModelName { get; init; }
        public string? BrandName { get; init; }
        public DateTime? InstallationDate { get; init; }
        public decimal? TotalDistanceKm { get; init; }
        public decimal? BatteryLevel { get; init; }
        public int? GsmSignal { get; init; }
        public string? Address { get; init; }
        public bool GpsOnline { get; init; }
        public decimal? Heading { get; init; }
        public decimal? FuelLevel { get; init; }
        public int? TraccarDeviceId { get; init; }
    }

    public async Task<ApiResponse<VehicleGpsDto>> Handle(
        GetVehicleGpsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<VehicleGpsRow>(
            new CommandDefinition(
                @"SELECT v.GpsDeviceId,
                         gd.Name AS DeviceName, gd.UniqueId, gd.IsActive, gd.LastSeenAt, gd.LastIgnition,
                         vcl.Latitude, vcl.Longitude, vcl.Speed, vcl.LastUpdate,
                         gd.SimNumber, COALESCE(tm.Name, gd.Model, gd.Name) AS ModelName, tb.Name AS BrandName,
                         gd.InstallationDate,
                         vcl.TotalDistanceKm, vcl.BatteryLevel, vcl.GsmSignal, vcl.Address,
                         CASE
                             WHEN (gd.LastSeenAt IS NOT NULL AND gd.LastSeenAt > DATEADD(minute, -30, GETUTCDATE()))
                               OR (vcl.LastUpdate IS NOT NULL AND vcl.LastUpdate > DATEADD(minute, -30, GETUTCDATE()))
                             THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT)
                         END AS GpsOnline,
                         vcl.Heading, vcl.FuelLevel,
                         gd.TraccarDeviceId
                  FROM Vehicles v
                  LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
                  LEFT JOIN TrackerModels tm ON tm.Id = gd.TrackerModelId
                  LEFT JOIN TrackerBrands tb ON tb.Id = tm.TrackerBrandId
                  LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                  WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0",
                new { request.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Vehicle", request.VehicleId);

        row = await OverlayLiveTraccarAsync(row, cancellationToken);

        var lastUpdate = GpsUtcDateTime.AsUtc(row.LastUpdate);
        var lastSeen = GpsUtcDateTime.AsUtc(row.LastSeenAt);
        var online = IsOnline(lastUpdate, lastSeen);

        var dto = new VehicleGpsDto(
            row.GpsDeviceId,
            row.DeviceName,
            row.UniqueId,
            row.IsActive,
            lastSeen,
            row.LastIgnition,
            row.Latitude,
            row.Longitude,
            row.Speed,
            lastUpdate,
            row.SimNumber,
            row.ModelName,
            row.BrandName,
            GpsUtcDateTime.AsUtc(row.InstallationDate),
            row.TotalDistanceKm,
            row.BatteryLevel,
            row.GsmSignal,
            row.Address,
            online,
            row.Heading,
            row.FuelLevel);

        return ApiResponse<VehicleGpsDto>.SuccessResponse(dto);
    }

    private static bool IsOnline(DateTime? lastUpdate, DateTime? lastSeen)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        return (lastUpdate.HasValue && lastUpdate.Value > cutoff)
            || (lastSeen.HasValue && lastSeen.Value > cutoff);
    }

    private async Task<VehicleGpsRow> OverlayLiveTraccarAsync(
        VehicleGpsRow row,
        CancellationToken cancellationToken)
    {
        var opts = traccarOptions.Value;
        if (!opts.Enabled || !opts.IsConfigured || row.TraccarDeviceId is null or <= 0)
            return row;

        try
        {
            // Single-device fetch + 2.5s budget — never block the profile on a slow Traccar round-trip.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(2500));

            var pos = await traccarClient.GetLatestPositionByDeviceAsync(
                row.TraccarDeviceId.Value, timeoutCts.Token);
            if (pos is null || (!pos.Valid && pos.Latitude == 0 && pos.Longitude == 0))
                return row;

            var fixTime = pos.FixTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(pos.FixTime, DateTimeKind.Utc)
                : pos.FixTime.ToUniversalTime();

            // Prefer Traccar only when it is at least as fresh as the local cache.
            if (row.LastUpdate is { } localTs)
            {
                var localUtc = localTs.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(localTs, DateTimeKind.Utc)
                    : localTs.ToUniversalTime();
                if (fixTime < localUtc.AddSeconds(-5))
                    return row;
            }

            var speedKmh = (decimal)(pos.Speed * 1.852);
            var ignition = pos.Attributes?.Ignition ?? row.LastIgnition;
            var battery = pos.Attributes?.BatteryLevel ?? row.BatteryLevel;
            var gsm = pos.Attributes?.Rssi ?? row.GsmSignal;
            var totalDistanceKm = pos.Attributes?.TotalDistance is { } meters
                ? meters / 1000m
                : row.TotalDistanceKm;
            var address = !string.IsNullOrWhiteSpace(pos.Address) ? pos.Address : row.Address;

            // Return live overlay only — background Traccar sync keeps SQL warm (no ingest on read path).
            return new VehicleGpsRow
            {
                GpsDeviceId = row.GpsDeviceId,
                DeviceName = row.DeviceName,
                UniqueId = row.UniqueId,
                IsActive = row.IsActive,
                LastSeenAt = fixTime,
                LastIgnition = ignition,
                Latitude = pos.Latitude,
                Longitude = pos.Longitude,
                Speed = speedKmh,
                LastUpdate = fixTime,
                SimNumber = row.SimNumber,
                ModelName = row.ModelName,
                BrandName = row.BrandName,
                InstallationDate = row.InstallationDate,
                TotalDistanceKm = totalDistanceKm,
                BatteryLevel = battery,
                GsmSignal = gsm,
                Address = address,
                GpsOnline = true,
                Heading = (decimal)pos.Course,
                FuelLevel = pos.Attributes?.Fuel ?? row.FuelLevel,
                TraccarDeviceId = row.TraccarDeviceId
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Traccar timed out — fall back to SQL snapshot.
            return row;
        }
        catch
        {
            return row;
        }
    }
}
