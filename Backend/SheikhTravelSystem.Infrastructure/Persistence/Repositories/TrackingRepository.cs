using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class TrackingRepository(IDbConnectionFactory dbFactory) : ITrackingRepository
{
    public async Task InsertLocationAsync(
        int vehicleId,
        int? driverId,
        int? bookingId,
        double latitude,
        double longitude,
        decimal speed,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
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
    }

    public async Task<IReadOnlyList<TrackingDto>> GetLiveAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tracking = await connection.QueryAsync<TrackingDto>(
            new CommandDefinition(
                @"SELECT t.Id, t.VehicleId, t.DriverId, t.BookingId, t.Latitude, t.Longitude, t.Speed, t.Timestamp
                  FROM VehicleTracking t
                  INNER JOIN (
                      SELECT VehicleId, MAX(Timestamp) AS MaxTimestamp
                      FROM VehicleTracking
                      WHERE Timestamp > DATEADD(MINUTE, -10, GETUTCDATE()) AND IsDeleted = 0
                      GROUP BY VehicleId
                  ) latest ON t.VehicleId = latest.VehicleId AND t.Timestamp = latest.MaxTimestamp",
                cancellationToken: cancellationToken));
        return tracking.ToList();
    }

    public async Task<IReadOnlyList<TrackingDto>> GetHistoryAsync(
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var history = await connection.QueryAsync<TrackingDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, DriverId, BookingId, Latitude, Longitude, Speed, Timestamp
                  FROM VehicleTracking
                  WHERE VehicleId = @VehicleId AND Timestamp BETWEEN @FromDate AND @ToDate AND IsDeleted = 0
                  ORDER BY Timestamp DESC",
                new { VehicleId = vehicleId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));
        return history.ToList();
    }
}
