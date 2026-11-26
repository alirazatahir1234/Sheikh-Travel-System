using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsTripPersistenceService(
    IFleetTripModeRepository fleetTripModeRepository,
    IDbConnectionFactory dbFactory,
    ITenantTimeZoneResolver timeZoneResolver) : IGpsTripPersistenceService
{
    private const string DailyAutoSource = "DailyAuto";
    private const string SegmentSource = "Segment";

    public async Task TryPersistRecentTripsAsync(
        IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        var mode = await fleetTripModeRepository.GetModeForVehicleAsync(vehicleId, cancellationToken);
        if (mode == FleetTripCreationMode.AutoGpsDaily)
        {
            await PersistAutoDailyTripsForVehicleAsync(connection, vehicleId, cancellationToken);
            return;
        }

        await PersistSegmentTripsAsync(connection, vehicleId, cancellationToken);
    }

    public async Task SyncAutoDailyTripsAsync(CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var vehicleIds = (await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT v.Id
            FROM Vehicles v
            INNER JOIN TenantGpsSettings g ON g.TenantId = v.TenantId
            WHERE v.IsDeleted = 0 AND g.TripCreationMode = 'AutoGpsDaily'
            """,
            cancellationToken: cancellationToken))).ToList();

        foreach (var vehicleId in vehicleIds)
        {
            await PersistAutoDailyTripsForVehicleAsync(connection, vehicleId, cancellationToken);
        }
    }

    private async Task PersistSegmentTripsAsync(
        IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        var lastTripEnd = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT MAX(EndTime) FROM GpsTrips WHERE VehicleId = @VehicleId AND DetectionSource = @Source",
            new { VehicleId = vehicleId, Source = SegmentSource },
            cancellationToken: cancellationToken));

        var fromDate = lastTripEnd ?? DateTime.UtcNow.AddHours(-24);
        var toDate = DateTime.UtcNow;

        var positions = await LoadPositionsAsync(connection, vehicleId, fromDate, toDate, cancellationToken);
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
                  WHERE VehicleId = @VehicleId AND StartTime = @StartTime AND EndTime = @EndTime
                    AND DetectionSource = @Source",
                new { trip.VehicleId, trip.StartTime, trip.EndTime, Source = SegmentSource },
                cancellationToken: cancellationToken));

            if (exists > 0)
            {
                continue;
            }

            await InsertTripAsync(connection, trip, SegmentSource, localTripDate: null, cancellationToken);
        }
    }

    private async Task PersistAutoDailyTripsForVehicleAsync(
        IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        var ctx = await fleetTripModeRepository.GetVehicleTenantContextAsync(vehicleId, cancellationToken);
        if (ctx.TenantId <= 0)
        {
            return;
        }

        // Daily trip boundaries follow the vehicle's branch timezone, else the tenant's, else the platform default.
        var timeZone = TenantTimeZoneHelper.Resolve(
            await timeZoneResolver.GetVehicleTimeZoneIdAsync(vehicleId, ctx.TenantId, cancellationToken),
            timeZoneResolver.DefaultTimeZoneId);
        var todayLocal = TenantTimeZoneHelper.UtcToLocalDate(DateTime.UtcNow, timeZone);
        var dates = new[] { todayLocal.AddDays(-1), todayLocal };

        var rangeStart = TimeZoneInfo.ConvertTimeToUtc(
            dates[0].ToDateTime(TimeOnly.MinValue), timeZone);
        var rangeEnd = DateTime.UtcNow;

        var positions = await LoadPositionsAsync(connection, vehicleId, rangeStart, rangeEnd, cancellationToken);
        if (positions.Count == 0)
        {
            return;
        }

        var vehicleName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Name FROM Vehicles WHERE Id = @Id",
            new { Id = vehicleId },
            cancellationToken: cancellationToken));

        var deviceId = positions.LastOrDefault()?.GpsDeviceId;

        foreach (var localDate in dates)
        {
            var trip = GpsDailyTripDetector.DetectDailyTrip(
                vehicleId, vehicleName, deviceId, positions, localDate, timeZone);
            if (trip is null)
            {
                continue;
            }

            await UpsertDailyAutoTripAsync(connection, trip, localDate, cancellationToken);
        }
    }

    private static async Task UpsertDailyAutoTripAsync(
        IDbConnection connection,
        GpsTripDto trip,
        DateOnly localTripDate,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE GpsTrips AS target
            USING (SELECT @VehicleId AS VehicleId, @LocalTripDate AS LocalTripDate) AS src
            ON target.VehicleId = src.VehicleId
               AND target.LocalTripDate = src.LocalTripDate
               AND target.DetectionSource = @Source
            WHEN MATCHED THEN
                UPDATE SET
                    GpsDeviceId = @GpsDeviceId,
                    StartTime = @StartTime,
                    EndTime = @EndTime,
                    DistanceKm = @DistanceKm,
                    AvgSpeedKmh = @AvgSpeedKmh,
                    MaxSpeedKmh = @MaxSpeedKmh,
                    DurationMinutes = @DurationMinutes
            WHEN NOT MATCHED THEN
                INSERT (VehicleId, BookingId, GpsDeviceId, StartTime, EndTime, DistanceKm, AvgSpeedKmh,
                        MaxSpeedKmh, DurationMinutes, DetectionSource, LocalTripDate, CreatedAt)
                VALUES (@VehicleId, NULL, @GpsDeviceId, @StartTime, @EndTime, @DistanceKm, @AvgSpeedKmh,
                        @MaxSpeedKmh, @DurationMinutes, @Source, @LocalTripDate, GETUTCDATE());
            """,
            new
            {
                trip.VehicleId,
                LocalTripDate = localTripDate.ToDateTime(TimeOnly.MinValue),
                Source = DailyAutoSource,
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

    private static async Task InsertTripAsync(
        IDbConnection connection,
        GpsTripDto trip,
        string detectionSource,
        DateOnly? localTripDate,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO GpsTrips
              (VehicleId, BookingId, GpsDeviceId, StartTime, EndTime, DistanceKm, AvgSpeedKmh, MaxSpeedKmh,
               DurationMinutes, DetectionSource, LocalTripDate, CreatedAt)
              VALUES (@VehicleId, @BookingId, @GpsDeviceId, @StartTime, @EndTime, @DistanceKm, @AvgSpeedKmh,
                      @MaxSpeedKmh, @DurationMinutes, @DetectionSource, @LocalTripDate, GETUTCDATE())",
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
                trip.DurationMinutes,
                DetectionSource = detectionSource,
                LocalTripDate = localTripDate?.ToDateTime(TimeOnly.MinValue)
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<List<PositionDto>> LoadPositionsAsync(
        IDbConnection connection,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<GpsPositionHistoryRow>(new CommandDefinition(
            @"SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                     Heading, Altitude, Ignition, RecordedAt AS Timestamp, Address
              FROM GpsPositions
              WHERE VehicleId = @VehicleId AND RecordedAt BETWEEN @FromDate AND @ToDate
              ORDER BY RecordedAt ASC",
            new { VehicleId = vehicleId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        return rows.Select(GpsPositionHistoryMapper.ToPositionDto).ToList();
    }
}
