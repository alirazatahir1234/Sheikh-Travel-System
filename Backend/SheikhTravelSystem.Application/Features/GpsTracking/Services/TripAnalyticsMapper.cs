using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TripAnalyticsMapper
{
    private const double KnotsToKmh = 1.852;

    public static TripReplayPositionDto ToReplayPosition(TraccarPosition position)
    {
        decimal? odometerKm = position.Attributes?.TotalDistance is { } meters
            ? Math.Round(meters / 1000m, 3)
            : null;

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
            position.Attributes?.Rssi,
            odometerKm);
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
            position.Address,
            position.BatteryLevel,
            position.GsmSignal,
            position.TotalDistanceKm);
    }

    public static TripEventDto ToEventDto(TraccarEvent evt, string? geofenceName = null)
    {
        var label = FormatEventLabel(evt.Type, geofenceName);
        return new TripEventDto(
            evt.EventTime,
            evt.Type,
            evt.Latitude,
            evt.Longitude,
            evt.Address,
            evt.SpeedKnots is null ? null : Math.Round((decimal)(evt.SpeedKnots.Value * KnotsToKmh), 1),
            evt.GeofenceId,
            geofenceName,
            label);
    }

    public static string FormatEventLabel(string type, string? geofenceName)
    {
        if (type.Contains("geofenceEnter", StringComparison.OrdinalIgnoreCase))
            return geofenceName is null ? "Entered geofence" : $"Entered {geofenceName}";
        if (type.Contains("geofenceExit", StringComparison.OrdinalIgnoreCase))
            return geofenceName is null ? "Exited geofence" : $"Exited {geofenceName}";
        if (type.Contains("alarm", StringComparison.OrdinalIgnoreCase))
            return "Alarm";
        if (type.Contains("overspeed", StringComparison.OrdinalIgnoreCase))
            return "Overspeed";
        return type;
    }

    public static double? ComputeOdometerMileageKm(IReadOnlyList<TripReplayPositionDto> route)
    {
        if (route.Count < 2) return null;
        var first = route[0].TotalDistanceKm;
        var last = route[^1].TotalDistanceKm;
        if (first is null || last is null || last < first) return null;
        return Math.Round((double)(last.Value - first.Value), 1);
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

    /// <summary>
    /// Traccar report durations are usually milliseconds. Small values may be seconds.
    /// Returns whole minutes.
    /// </summary>
    private static int ToDurationMinutes(long engineHours)
    {
        if (engineHours <= 0) return 0;
        // Milliseconds (≥ ~1.7 min)
        if (engineHours >= 100_000)
            return (int)(engineHours / 60_000);
        // Seconds (legacy / some protocols)
        if (engineHours >= 3_600)
            return (int)(engineHours / 60);
        // Already minutes
        return (int)engineHours;
    }

    /// <summary>
    /// History-range statistics: prefer odometer distance, stop-based idle, and clamp driving
    /// time to the selected window (Traccar engineHours can report lifetime totals).
    /// </summary>
    public static TripAnalyticsSummaryDto BuildHistoryStatistics(
        TripAnalyticsSummaryDto? baseSummary,
        IReadOnlyList<TripReplayPositionDto> route,
        IReadOnlyList<TripStopDto> stops,
        DateTime fromDate,
        DateTime toDate,
        double? mileageKm)
    {
        var rangeMinutes = Math.Max(1, (int)Math.Ceiling((toDate - fromDate).TotalMinutes));
        var stopIdle = Math.Min(rangeMinutes, stops.Sum(s => Math.Max(0, s.DurationMinutes)));

        int drivingMinutes;
        if (baseSummary is not null && baseSummary.DrivingMinutes > 0 && baseSummary.DrivingMinutes <= rangeMinutes)
        {
            drivingMinutes = baseSummary.DrivingMinutes;
        }
        else
        {
            drivingMinutes = Math.Max(0, rangeMinutes - stopIdle);
            if (route.Count >= 2)
            {
                var first = route[0].Timestamp;
                var last = route[^1].Timestamp;
                var span = Math.Max(1, (int)Math.Ceiling((last - first).TotalMinutes));
                drivingMinutes = Math.Min(drivingMinutes, Math.Max(0, Math.Min(span, rangeMinutes) - Math.Min(stopIdle, span)));
            }
        }

        var distanceKm = mileageKm
            ?? baseSummary?.DistanceKm
            ?? (BuildReplaySummary([], route)?.DistanceKm ?? 0);

        decimal avgSpeed = baseSummary?.AvgSpeedKmh ?? 0;
        decimal maxSpeed = baseSummary?.MaxSpeedKmh ?? 0;
        if ((avgSpeed <= 0 || maxSpeed <= 0) && route.Count > 0)
        {
            var speeds = route.Select(p => (double)p.SpeedKmh).ToList();
            if (avgSpeed <= 0) avgSpeed = Math.Round((decimal)speeds.Average(), 1);
            if (maxSpeed <= 0) maxSpeed = Math.Round((decimal)speeds.Max(), 1);
        }

        return new TripAnalyticsSummaryDto(
            baseSummary?.TripCount ?? 0,
            Math.Round(distanceKm, 2),
            drivingMinutes,
            stopIdle,
            baseSummary?.FuelLiters,
            avgSpeed,
            maxSpeed,
            stops.Count,
            baseSummary?.OverspeedCount ?? 0,
            baseSummary?.HarshBrakeCount ?? 0,
            baseSummary?.HarshAccelCount ?? 0,
            Math.Round(drivingMinutes / 60m, 1));
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

        var firstOd = points[0].TotalDistanceKm;
        var lastOd = points[^1].TotalDistanceKm;

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

        if (result.Count >= 2)
        {
            result[0] = result[0] with { TotalDistanceKm = firstOd ?? result[0].TotalDistanceKm };
            result[^1] = result[^1] with { TotalDistanceKm = lastOd ?? result[^1].TotalDistanceKm };
        }

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
