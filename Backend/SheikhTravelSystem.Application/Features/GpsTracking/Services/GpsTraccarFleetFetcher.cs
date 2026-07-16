using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Shared device-resolution helper for fleet-wide Traccar fan-out queries (distance/speed/idle/stop
/// analytics) — resolves which vehicles in a filtered set are Traccar-linked so callers can fetch
/// live per-device data (uncapped: see GetFleetTripSummaryQueryHandler for why the old 20-device
/// cap was removed) and flag results as partial when some vehicles have no Traccar link at all.
/// </summary>
public static class GpsTraccarFleetFetcher
{
    public static async Task<Dictionary<int, int>> ResolveVehicleToDeviceMapAsync(
        System.Data.IDbConnection connection, int tenantId, IEnumerable<int> vehicleIds, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(int VehicleId, int TraccarDeviceId)>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, d.TraccarDeviceId
            FROM Vehicles v
            INNER JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
              AND v.Id IN @VehicleIds
            """,
            new { TenantId = tenantId, VehicleIds = vehicleIds.Distinct().ToList() },
            cancellationToken: cancellationToken));

        return rows.ToDictionary(r => r.VehicleId, r => r.TraccarDeviceId);
    }

    /// <summary>True if any vehicle in the set has no Traccar link — callers should surface this as partial data, not silently omit it.</summary>
    public static async Task<bool> HasNonTraccarVehicleAsync(
        System.Data.IDbConnection connection, int tenantId, IEnumerable<int> vehicleIds, CancellationToken cancellationToken)
    {
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Id IN @VehicleIds
              AND (d.Id IS NULL OR d.TraccarDeviceId IS NULL)
            """,
            new { TenantId = tenantId, VehicleIds = vehicleIds.Distinct().ToList() },
            cancellationToken: cancellationToken));

        return count > 0;
    }
}
