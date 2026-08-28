using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TraccarTripMapper
{
    private const double KnotsToKmh = 1.852;

    /// <summary>
    /// Trips below this distance are treated as stops / idle segments — not moving trips.
    /// GPS noise can still report a high instantaneous max speed with ~0 km travelled.
    /// </summary>
    public const double MinMovingDistanceKm = 0.2;

    public static GpsTripDto ToGpsTripDto(
        TraccarTrip trip,
        int vehicleId,
        string? vehicleName,
        int? gpsDeviceId,
        string? deviceName,
        string? plateNumber = null)
    {
        var start = trip.StartTime;
        var durationMinutes = ToDurationMinutes(trip.Duration);
        var distanceKm = Math.Round(trip.Distance / 1000.0, 2);
        var (avg, max, status) = NormalizeSpeeds(
            distanceKm,
            durationMinutes,
            trip.AverageSpeed * KnotsToKmh,
            trip.MaxSpeed * KnotsToKmh);

        return new GpsTripDto(
            vehicleId,
            vehicleName,
            gpsDeviceId,
            start,
            trip.EndTime,
            distanceKm,
            avg,
            max,
            durationMinutes,
            deviceName ?? trip.DeviceName,
            trip.StartAddress,
            trip.EndAddress,
            trip.DriverName,
            trip.SpentFuel,
            plateNumber,
            TripKeyHelper.Build(vehicleId, start),
            status,
            trip.StartLat,
            trip.StartLon,
            trip.EndLat,
            trip.EndLon);
    }

    /// <summary>
    /// Reconcile distance/time average with reported max speed, and classify stop vs completed move.
    /// </summary>
    public static (decimal AvgSpeedKmh, decimal MaxSpeedKmh, string Status) NormalizeSpeeds(
        double distanceKm,
        int durationMinutes,
        double reportedAvgKmh,
        double reportedMaxKmh)
    {
        if (distanceKm < MinMovingDistanceKm)
        {
            // Stationary / idle segment — don't surface GPS noise as max speed.
            return (0m, 0m, "Stop");
        }

        var hours = Math.Max(durationMinutes, 1) / 60.0;
        var computedAvg = distanceKm / hours;
        // Prefer distance/time average; Traccar avg can disagree with max when units/noise differ.
        var avg = Math.Round((decimal)computedAvg, 1);
        var max = Math.Round((decimal)Math.Max(reportedMaxKmh, reportedAvgKmh), 1);
        if (max < avg)
        {
            max = avg;
        }

        return (avg, max, "Completed");
    }

    /// <summary>
    /// Traccar OpenAPI reports duration in seconds; older servers may return milliseconds.
    /// </summary>
    private static int ToDurationMinutes(int duration)
    {
        if (duration <= 0)
        {
            return 1;
        }

        var minutes = duration >= 100_000
            ? duration / 60_000
            : duration / 60;

        return Math.Max(1, minutes);
    }

    /// <summary>Attach a stable composite key when missing (e.g. rows loaded from SQL).</summary>
    public static GpsTripDto Enrich(GpsTripDto trip)
    {
        var key = string.IsNullOrWhiteSpace(trip.TripKey)
            ? TripKeyHelper.Build(trip.VehicleId, trip.StartTime)
            : trip.TripKey;
        var (avg, max, status) = NormalizeSpeeds(
            trip.DistanceKm,
            trip.DurationMinutes,
            (double)trip.AvgSpeedKmh,
            (double)trip.MaxSpeedKmh);
        var resolvedStatus = string.IsNullOrWhiteSpace(trip.Status) || trip.Status == "Completed"
            ? status
            : trip.Status;
        // Re-apply stop classification even when Status was already "Completed".
        if (trip.DistanceKm < MinMovingDistanceKm)
        {
            resolvedStatus = "Stop";
        }

        return trip with
        {
            TripKey = key,
            Status = resolvedStatus,
            AvgSpeedKmh = avg,
            MaxSpeedKmh = max,
        };
    }

    public static List<GpsTripDto> EnrichAll(IEnumerable<GpsTripDto> trips)
        => trips.Select(Enrich).ToList();
}
