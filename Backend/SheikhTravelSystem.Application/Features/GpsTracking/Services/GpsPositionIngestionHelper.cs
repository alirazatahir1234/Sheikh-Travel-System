using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class GpsPositionIngestionHelper
{
    private const decimal MovingSpeedKmh = 5m;

    public static async Task<int?> ResolveActiveBookingIdAsync(
        System.Data.IDbConnection connection,
        int vehicleId,
        int? explicitBookingId,
        CancellationToken cancellationToken)
    {
        if (explicitBookingId.HasValue)
        {
            return explicitBookingId;
        }

        var confirmed = (int)BookingStatus.Confirmed;
        var started = (int)BookingStatus.Started;

        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            @"SELECT TOP 1 Id FROM Bookings
              WHERE VehicleId = @VehicleId AND IsDeleted = 0
              AND Status IN (@Confirmed, @Started)
              AND PickupTime <= DATEADD(HOUR, 2, GETUTCDATE())
              AND (DropoffTime IS NULL OR DropoffTime >= DATEADD(HOUR, -1, GETUTCDATE()))
              ORDER BY PickupTime DESC",
            new { VehicleId = vehicleId, Confirmed = confirmed, Started = started },
            cancellationToken: cancellationToken));
    }

    public static async Task IngestAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime recordedAt,
        CancellationToken cancellationToken)
    {
        var bookingId = await ResolveActiveBookingIdAsync(connection, dto.VehicleId, dto.BookingId, cancellationToken);
        var driverId = dto.DriverId;

        if (driverId is null && bookingId.HasValue)
        {
            driverId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT DriverId FROM Bookings WHERE Id = @Id",
                new { Id = bookingId.Value },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO GpsPositions
              (VehicleId, GpsDeviceId, DriverId, BookingId, Latitude, Longitude, Speed, Heading, Altitude, Ignition, RecordedAt, CreatedAt)
              VALUES (@VehicleId, @GpsDeviceId, @DriverId, @BookingId, @Latitude, @Longitude, @Speed, @Heading, @Altitude, @Ignition, @RecordedAt, @RecordedAt)",
            new
            {
                dto.VehicleId,
                dto.GpsDeviceId,
                DriverId = driverId,
                BookingId = bookingId,
                dto.Latitude,
                dto.Longitude,
                dto.Speed,
                dto.Heading,
                dto.Altitude,
                dto.Ignition,
                RecordedAt = recordedAt
            },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE VehicleCurrentLocation AS target
            USING (SELECT @VehicleId AS VehicleId) AS source
            ON target.VehicleId = source.VehicleId
            WHEN MATCHED THEN
              UPDATE SET GpsDeviceId = @GpsDeviceId, DriverId = @DriverId, BookingId = @BookingId,
                Latitude = @Latitude, Longitude = @Longitude, Speed = @Speed, Heading = @Heading,
                Ignition = @Ignition, LastUpdate = @LastUpdate
            WHEN NOT MATCHED THEN
              INSERT (VehicleId, GpsDeviceId, DriverId, BookingId, Latitude, Longitude, Speed, Heading, Ignition, LastUpdate)
              VALUES (@VehicleId, @GpsDeviceId, @DriverId, @BookingId, @Latitude, @Longitude, @Speed, @Heading, @Ignition, @LastUpdate);
            """,
            new
            {
                dto.VehicleId,
                dto.GpsDeviceId,
                DriverId = driverId,
                BookingId = bookingId,
                dto.Latitude,
                dto.Longitude,
                dto.Speed,
                dto.Heading,
                dto.Ignition,
                LastUpdate = recordedAt
            },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO VehicleTracking
              (VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed, Heading, Altitude, Ignition, Timestamp, CreatedAt, IsDeleted)
              VALUES (@VehicleId, @DriverId, @BookingId, @GpsDeviceId, @Latitude, @Longitude, @Speed, @Heading, @Altitude, @Ignition, @Timestamp, @Timestamp, 0)",
            new
            {
                dto.VehicleId,
                DriverId = driverId,
                BookingId = bookingId,
                dto.GpsDeviceId,
                dto.Latitude,
                dto.Longitude,
                dto.Speed,
                dto.Heading,
                dto.Altitude,
                dto.Ignition,
                Timestamp = recordedAt
            },
            cancellationToken: cancellationToken));
    }

    public static bool ShouldAttemptTripPersistence(decimal speed, bool? ignition, decimal? previousSpeed)
    {
        var stopped = ignition == false || speed <= MovingSpeedKmh;
        var wasMoving = previousSpeed is > MovingSpeedKmh;
        return stopped && wasMoving;
    }
}
