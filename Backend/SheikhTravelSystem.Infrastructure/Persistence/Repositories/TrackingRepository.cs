using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class TrackingRepository(IDbConnectionFactory dbFactory) : ITrackingRepository
{
    public async Task<bool> InsertLocationAsync(
        int tenantId,
        int vehicleId,
        int? driverId,
        int? bookingId,
        double latitude,
        double longitude,
        decimal speed,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var owned = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                      SELECT 1 FROM Vehicles
                      WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
                  ) THEN 1 ELSE 0 END",
                new { VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!owned)
            return false;

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO VehicleTracking (VehicleId, DriverId, BookingId, Latitude, Longitude, Speed, Timestamp, CreatedAt, IsDeleted)
                  VALUES (@VehicleId, @DriverId, @BookingId, @Latitude, @Longitude, @Speed, @Timestamp, @CreatedAt, 0)",
                new
                {
                    VehicleId = vehicleId,
                    DriverId = driverId,
                    BookingId = bookingId,
                    Latitude = latitude,
                    Longitude = longitude,
                    Speed = speed,
                    Timestamp = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return true;
    }

    public async Task<IReadOnlyList<TrackingDto>> GetLiveAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tracking = await connection.QueryAsync<TrackingDto>(
            new CommandDefinition(
                @"SELECT t.Id, t.VehicleId, t.DriverId, t.BookingId, t.Latitude, t.Longitude, t.Speed, t.Timestamp
                  FROM VehicleTracking t
                  INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                  INNER JOIN (
                      SELECT vt.VehicleId, MAX(vt.Timestamp) AS MaxTimestamp
                      FROM VehicleTracking vt
                      INNER JOIN Vehicles vv ON vv.Id = vt.VehicleId AND vv.TenantId = @TenantId AND vv.IsDeleted = 0
                      WHERE vt.Timestamp > DATEADD(MINUTE, -10, GETUTCDATE()) AND vt.IsDeleted = 0
                      GROUP BY vt.VehicleId
                  ) latest ON t.VehicleId = latest.VehicleId AND t.Timestamp = latest.MaxTimestamp
                  WHERE t.IsDeleted = 0",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        return tracking.ToList();
    }

    public async Task<IReadOnlyList<TrackingDto>> GetHistoryAsync(
        int tenantId,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var history = await connection.QueryAsync<TrackingDto>(
            new CommandDefinition(
                @"SELECT t.Id, t.VehicleId, t.DriverId, t.BookingId, t.Latitude, t.Longitude, t.Speed, t.Timestamp
                  FROM VehicleTracking t
                  INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                  WHERE t.VehicleId = @VehicleId
                    AND t.Timestamp BETWEEN @FromDate AND @ToDate
                    AND t.IsDeleted = 0
                  ORDER BY t.Timestamp DESC",
                new { TenantId = tenantId, VehicleId = vehicleId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));
        return history.ToList();
    }
}
