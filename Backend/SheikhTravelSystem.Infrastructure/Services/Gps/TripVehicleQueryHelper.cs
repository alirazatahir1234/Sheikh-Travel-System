using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class TripVehicleQueryHelper(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext) : ITripVehicleQueryHelper
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(30);

    public async Task<VehicleTripSource?> ResolveVehicleByTraccarDeviceIdAsync(
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

    public async Task<VehicleTripSource?> ResolveVehicleAsync(
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

    public async Task<TripDeviceContextDto?> BuildContextAsync(
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
