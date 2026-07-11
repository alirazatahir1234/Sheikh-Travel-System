using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TraccarTripMapper
{
    private const double KnotsToKmh = 1.852;

    public static GpsTripDto ToGpsTripDto(
        TraccarTrip trip,
        int vehicleId,
        string? vehicleName,
        int? gpsDeviceId,
        string? deviceName,
        string? plateNumber = null)
    {
        var start = trip.StartTime;
        return new GpsTripDto(
            vehicleId,
            vehicleName,
            gpsDeviceId,
            start,
            trip.EndTime,
            Math.Round(trip.Distance / 1000.0, 2),
            Math.Round((decimal)(trip.AverageSpeed * KnotsToKmh), 1),
            Math.Round((decimal)(trip.MaxSpeed * KnotsToKmh), 1),
            ToDurationMinutes(trip.Duration),
            deviceName ?? trip.DeviceName,
            trip.StartAddress,
            trip.EndAddress,
            trip.DriverName,
            trip.SpentFuel,
            plateNumber,
            TripKeyHelper.Build(vehicleId, start),
            "Completed");
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
        var status = string.IsNullOrWhiteSpace(trip.Status) ? "Completed" : trip.Status;
        return trip with { TripKey = key, Status = status };
    }

    public static List<GpsTripDto> EnrichAll(IEnumerable<GpsTripDto> trips)
        => trips.Select(Enrich).ToList();
}
