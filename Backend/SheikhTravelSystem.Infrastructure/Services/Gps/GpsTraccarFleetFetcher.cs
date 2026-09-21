using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsTraccarFleetFetcher(IDbConnectionFactory dbFactory) : IGpsTraccarFleetFetcher
{
    public async Task<Dictionary<int, int>> ResolveVehicleToDeviceMapAsync(
        int tenantId, IEnumerable<int> vehicleIds, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
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

    public async Task<bool> HasNonTraccarVehicleAsync(
        int tenantId, IEnumerable<int> vehicleIds, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
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
