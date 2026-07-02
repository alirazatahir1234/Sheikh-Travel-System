using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TripAnalyticsMapper
{
    private const double KnotsToKmh = 1.852;

    public static TripReplayPositionDto ToReplayPosition(TraccarPosition position)
    {
        return new TripReplayPositionDto(
            position.FixTime,
            position.Latitude,
            position.Longitude,
            Math.Round((decimal)(position.Speed * KnotsToKmh), 1),
            position.Course,
            position.Attributes?.Ignition,
            position.Altitude,
            position.Address,
            position.Attributes?.BatteryLevel,
            position.Attributes?.Rssi);
    }

    public static TripReplayPositionDto ToReplayPosition(PositionDto position)
    {
        return new TripReplayPositionDto(
            position.Timestamp,
            position.Latitude,
            position.Longitude,
            position.Speed,
            position.Heading,
            position.Ignition,
            position.Altitude,
            null,
            null,
            null);
    }

    public static TripEventDto ToEventDto(TraccarEvent evt)
    {
        return new TripEventDto(
            evt.EventTime,
            evt.Type,
            evt.Latitude,
            evt.Longitude,
            evt.Address,
            evt.SpeedKnots is null ? null : Math.Round((decimal)(evt.SpeedKnots.Value * KnotsToKmh), 1));
    }

    public static TripStopDto ToStopDto(TraccarStop stop)
    {
        var durationMinutes = stop.Duration >= 100_000
            ? stop.Duration / 60_000
            : Math.Max(1, stop.Duration / 60);

        return new TripStopDto(
            stop.StartTime,
            stop.EndTime,
            stop.Lat,
            stop.Lon,
            stop.Address,
            durationMinutes);
    }

    public static TripAnalyticsSummaryDto BuildSummary(
        IReadOnlyList<GpsTripDto> trips,
        IReadOnlyList<TraccarSummary> summaries,
        IReadOnlyList<TraccarStop> stops,
        IReadOnlyList<TraccarEvent> events)
    {
        var tripCount = trips.Count;

        var hasSummary = summaries.Count > 0;
        var distanceKm = hasSummary
            ? summaries.Sum(s => s.Distance / 1000.0)
            : tripCount > 0
                ? trips.Sum(t => t.DistanceKm)
                : 0;

        var drivingMinutes = hasSummary
            ? summaries.Sum(s => ToDurationMinutes(s.EngineHours))
            : tripCount > 0
                ? trips.Sum(t => t.DurationMinutes)
                : 0;

        var idleMinutes = stops.Sum(s =>
        {
            var d = s.Duration >= 100_000 ? s.Duration / 60_000 : s.Duration / 60;
            return Math.Max(0, d);
        });

        var avgSpeed = hasSummary
            ? summaries.Average(s => s.AverageSpeed * KnotsToKmh)
            : tripCount > 0
                ? trips.Average(t => (double)t.AvgSpeedKmh)
                : 0;

        var maxSpeed = hasSummary
            ? summaries.Max(s => s.MaxSpeed * KnotsToKmh)
            : tripCount > 0
                ? trips.Max(t => (double)t.MaxSpeedKmh)
                : 0;

        var fuel = summaries.Sum(s => s.SpentFuel);
        decimal? fuelLiters = fuel > 0 ? fuel : null;

        // Only meaningful when sourced from Traccar summaries — local trip-detection fallback has
        // no independent engine-on signal, so DrivingMinutes there is trip duration, not engine time.
        decimal? engineHours = hasSummary
            ? Math.Round(drivingMinutes / 60m, 1)
            : null;

        return new TripAnalyticsSummaryDto(
            tripCount,
            Math.Round(distanceKm, 2),
            drivingMinutes,
            idleMinutes,
            fuelLiters,
            Math.Round((decimal)avgSpeed, 1),
            Math.Round((decimal)maxSpeed, 1),
            stops.Count,
            CountEvents(events, "deviceOverspeed", "overspeed"),
            CountEvents(events, "hardBraking", "harshBraking", "braking"),
            CountEvents(events, "hardAcceleration", "harshAcceleration", "acceleration"),
            engineHours);
    }

    private static int ToDurationMinutes(long engineHours)
    {
        if (engineHours <= 0) return 0;
        return engineHours >= 100_000
            ? (int)(engineHours / 60_000)
            : (int)(engineHours / 60);
    }

    private static int CountEvents(IReadOnlyList<TraccarEvent> events, params string[] types)
    {
        return events.Count(e =>
            types.Any(t => e.Type.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Reduce replay payload size so the UI can render routes without freezing.</summary>
    public static List<TripReplayPositionDto> DownsampleReplay(List<TripReplayPositionDto> points, int maxPoints = 1500)
    {
        if (points.Count <= maxPoints) return points;

        var step = (double)points.Count / maxPoints;
        var result = new List<TripReplayPositionDto>(maxPoints + 1);
        for (var i = 0; i < maxPoints; i++)
        {
            var idx = Math.Min((int)Math.Floor(i * step), points.Count - 1);
            result.Add(points[idx]);
        }

        var last = points[^1];
        if (result[^1].Timestamp != last.Timestamp)
            result.Add(last);

        return result;
    }

    public static bool OverlapsWindow(DateTime start, DateTime end, DateTime windowFrom, DateTime windowTo)
        => start < windowTo && end > windowFrom;

    public static TripReplaySummaryDto? BuildReplaySummary(
        IReadOnlyList<TraccarSummary> summaries,
        IReadOnlyList<TripReplayPositionDto> route)
    {
        if (summaries.Count > 0)
        {
            var s = summaries[0];
            var drivingMinutes = ToDurationMinutes(s.EngineHours);
            return new TripReplaySummaryDto(
                Math.Round(s.Distance / 1000.0, 2),
                drivingMinutes,
                Math.Round((decimal)(s.AverageSpeed * KnotsToKmh), 1),
                Math.Round((decimal)(s.MaxSpeed * KnotsToKmh), 1),
                s.SpentFuel > 0 ? s.SpentFuel : null,
                Math.Round(drivingMinutes / 60m, 1));
        }

        if (route.Count < 2) return null;

        var speeds = route.Select(p => (double)p.SpeedKmh).ToList();
        var first = route[0].Timestamp;
        var last = route[^1].Timestamp;
        double distanceKm = 0;
        for (var i = 1; i < route.Count; i++)
        {
            distanceKm += HaversineKm(
                route[i - 1].Latitude, route[i - 1].Longitude,
                route[i].Latitude, route[i].Longitude);
        }

        return new TripReplaySummaryDto(
            Math.Round(distanceKm, 2),
            Math.Max(1, (int)(last - first).TotalMinutes),
            Math.Round((decimal)speeds.Average(), 1),
            Math.Round((decimal)speeds.Max(), 1),
            null);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
