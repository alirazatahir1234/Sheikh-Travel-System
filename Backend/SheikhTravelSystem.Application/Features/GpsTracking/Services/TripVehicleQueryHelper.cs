using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

internal static class TripVehicleQueryHelper
{
    internal static readonly TimeSpan MaxRange = TimeSpan.FromDays(30);
    internal static readonly TimeSpan MaxHistoryRange = TimeSpan.FromDays(366);
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(30);

    internal sealed record VehicleTripSource(
        int VehicleId,
        string? VehicleName,
        string? PlateNumber,
        int? GpsDeviceId,
        string? DeviceName,
        string? UniqueId,
        int? TraccarDeviceId,
        DateTime? LastSeenAt);

    internal static ApiResponse<T>? ValidateTripRequest<T>(int? vehicleId, DateTime fromDate, DateTime toDate)
    {
        if (fromDate > toDate)
        {
            return ApiResponse<T>.FailResponse("End Date cannot be earlier than Start Date.");
        }

        if (toDate - fromDate > MaxRange)
        {
            return ApiResponse<T>.FailResponse("Date range cannot exceed 30 days.");
        }

        if (!vehicleId.HasValue)
        {
            return ApiResponse<T>.FailResponse("Select a vehicle to view trips.");
        }

        return null;
    }

    internal static ApiResponse<T>? ValidateHistoryRequest<T>(
        int? vehicleId,
        DateTime fromDate,
        DateTime toDate,
        int? deviceId = null)
    {
        if (fromDate > toDate)
        {
            return ApiResponse<T>.FailResponse("End Date cannot be earlier than Start Date.");
        }

        if (toDate - fromDate > MaxHistoryRange)
        {
            return ApiResponse<T>.FailResponse("Date range cannot exceed 366 days.");
        }

        if (!vehicleId.HasValue && !deviceId.HasValue)
        {
            return ApiResponse<T>.FailResponse("Select a vehicle or device to view history.");
        }

        return null;
    }

    internal static async Task<VehicleTripSource?> ResolveVehicleByTraccarDeviceIdAsync(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        int traccarDeviceId,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        return await connection.QueryFirstOrDefaultAsync<VehicleTripSource>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.UniqueId, d.TraccarDeviceId, d.LastSeenAt
            FROM GpsDevices d
            INNER JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
            WHERE d.TraccarDeviceId = @TraccarDeviceId
              AND v.TenantId = @TenantId
              AND d.IsDeleted = 0
            """,
            new { TraccarDeviceId = traccarDeviceId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    internal static async Task<VehicleTripSource?> ResolveVehicleAsync(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        return await connection.QueryFirstOrDefaultAsync<VehicleTripSource>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.UniqueId, d.TraccarDeviceId, d.LastSeenAt
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    internal static async Task<TripDeviceContextDto?> BuildContextAsync(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.UniqueId, d.TraccarDeviceId, d.LastSeenAt,
                   vcl.Latitude AS LastLatitude, vcl.Longitude AS LastLongitude,
                   vcl.Speed AS LastSpeed, vcl.LastUpdate AS LastPositionAt, vcl.Ignition AS LastIgnition
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null) return null;

        DateTime? lastSeen = row.LastSeenAt ?? row.LastPositionAt;
        var isOnline = lastSeen.HasValue && DateTime.UtcNow - lastSeen.Value <= OnlineWindow;

        return new TripDeviceContextDto(
            (int)row.VehicleId,
            (string?)row.VehicleName,
            (string?)row.PlateNumber,
            (int?)row.GpsDeviceId,
            (string?)row.DeviceName,
            (string?)row.UniqueId,
            row.TraccarDeviceId is not null,
            isOnline,
            lastSeen,
            (double?)row.LastLatitude,
            (double?)row.LastLongitude,
            null,
            row.LastSpeed is null ? null : (decimal?)row.LastSpeed,
            (bool?)row.LastIgnition);
    }
}
