using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class GpsTripPersistenceService
{
    public static async Task TryPersistRecentTripsAsync(
        System.Data.IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        var lastTripEnd = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT MAX(EndTime) FROM GpsTrips WHERE VehicleId = @VehicleId",
            new { VehicleId = vehicleId },
            cancellationToken: cancellationToken));

        var fromDate = lastTripEnd ?? DateTime.UtcNow.AddHours(-24);
        var toDate = DateTime.UtcNow;

        var rows = await connection.QueryAsync<PositionDto>(new CommandDefinition(
            @"SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                     Heading, Altitude, Ignition, RecordedAt AS Timestamp
              FROM GpsPositions
              WHERE VehicleId = @VehicleId AND RecordedAt BETWEEN @FromDate AND @ToDate
              ORDER BY RecordedAt ASC",
            new { VehicleId = vehicleId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        var positions = rows.ToList();
        if (positions.Count < 2)
        {
            return;
        }

        var vehicleName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Name FROM Vehicles WHERE Id = @Id",
            new { Id = vehicleId },
            cancellationToken: cancellationToken));

        var deviceId = positions.LastOrDefault()?.GpsDeviceId;
        var detected = GpsTripDetector.DetectTrips(vehicleId, vehicleName, deviceId, positions);

        foreach (var trip in detected)
        {
            if (lastTripEnd.HasValue && trip.EndTime <= lastTripEnd.Value)
            {
                continue;
            }

            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1) FROM GpsTrips
                  WHERE VehicleId = @VehicleId AND StartTime = @StartTime AND EndTime = @EndTime",
                new { trip.VehicleId, trip.StartTime, trip.EndTime },
                cancellationToken: cancellationToken));

            if (exists > 0)
            {
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO GpsTrips
                  (VehicleId, BookingId, GpsDeviceId, StartTime, EndTime, DistanceKm, AvgSpeedKmh, MaxSpeedKmh, DurationMinutes, CreatedAt)
                  VALUES (@VehicleId, @BookingId, @GpsDeviceId, @StartTime, @EndTime, @DistanceKm, @AvgSpeedKmh, @MaxSpeedKmh, @DurationMinutes, GETUTCDATE())",
                new
                {
                    trip.VehicleId,
                    BookingId = (int?)null,
                    trip.GpsDeviceId,
                    trip.StartTime,
                    trip.EndTime,
                    trip.DistanceKm,
                    trip.AvgSpeedKmh,
                    trip.MaxSpeedKmh,
                    trip.DurationMinutes
                },
                cancellationToken: cancellationToken));
        }
    }
}
