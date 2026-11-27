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
        var label = FormatEventLabel(evt.Type, geofenceName, evt.Attributes?.Alarm);
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

    /// <summary>Names Traccar gives an alarm in <c>attributes.alarm</c>, as people say them.</summary>
    private static readonly Dictionary<string, string> AlarmLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sos"] = "SOS",
        ["powerCut"] = "Power cut",
        ["powerOff"] = "Power off",
        ["powerRestored"] = "Power restored",
        ["lowBattery"] = "Low battery",
        ["lowPower"] = "Low power",
        ["hardBraking"] = "Harsh braking",
        ["hardAcceleration"] = "Harsh acceleration",
        ["hardCornering"] = "Harsh cornering",
        ["accident"] = "Accident",
        ["tampering"] = "Tampering",
        ["removing"] = "Device removed",
        ["fatigueDriving"] = "Fatigue driving",
        ["jamming"] = "GPS jamming",
        ["overspeed"] = "Overspeed"
    };

    /// <param name="alarm">Traccar's <c>attributes.alarm</c>: the kind of an <c>alarm</c> event (sos, powerCut, …).</param>
    public static string FormatEventLabel(string type, string? geofenceName, string? alarm = null)
    {
        if (type.Contains("geofenceEnter", StringComparison.OrdinalIgnoreCase))
            return geofenceName is null ? "Entered geofence" : $"Entered {geofenceName}";
        if (type.Contains("geofenceExit", StringComparison.OrdinalIgnoreCase))
            return geofenceName is null ? "Exited geofence" : $"Exited {geofenceName}";
        if (type.Contains("alarm", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(alarm) ? "Alarm" : AlarmLabel(alarm.Trim());
        if (type.Contains("overspeed", StringComparison.OrdinalIgnoreCase))
            return "Overspeed";
        if (AlarmLabels.TryGetValue(type, out var named))
            return named;
        return type;
    }

    /// <summary>A known alarm by its name; any other camelCase kind as words ("doorOpen" → "Door open").</summary>
    private static string AlarmLabel(string alarm)
    {
        if (AlarmLabels.TryGetValue(alarm, out var named)) return named;
        var words = new System.Text.StringBuilder(alarm.Length + 4);
        for (var i = 0; i < alarm.Length; i++)
        {
            var c = alarm[i];
            if (c is '_' or '-') { words.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(alarm[i - 1])) words.Append(' ');
            words.Append(words.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
        }
        return words.ToString();
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
        var durationMinutes = StopDurationMinutes(stop.StartTime, stop.EndTime, stop.Duration);

        return new TripStopDto(
            stop.StartTime,
            stop.EndTime,
            stop.Lat,
            stop.Lon,
            stop.Address,
            durationMinutes);
    }

    /// <summary>
    /// Prefer wall-clock end−start. Fallback: Traccar Duration as ms when ≥1000, else seconds.
    /// </summary>
    public static int StopDurationMinutes(DateTime startTime, DateTime endTime, long durationRaw)
    {
        if (endTime > startTime)
            return Math.Max(0, (int)Math.Round((endTime - startTime).TotalMinutes));

        return DurationRawToMinutes(durationRaw);
    }

    /// <summary>
    /// Converts a Traccar duration field to whole minutes.
    /// Values ≥ 1000 are milliseconds (Traccar norm); smaller values are seconds.
    /// </summary>
    public static int DurationRawToMinutes(long durationRaw)
    {
        if (durationRaw <= 0) return 0;
        if (durationRaw >= 1000)
            return Math.Max(0, (int)Math.Round(durationRaw / 60_000.0));
        return Math.Max(0, (int)Math.Round(durationRaw / 60.0));
    }

    /// <summary>
    /// Sum of stop overlaps with [fromDate, toDate], capped to the window length.
    /// </summary>
    public static int ClippedIdleMinutes(
        IReadOnlyList<TripStopDto> stops,
        DateTime fromDate,
        DateTime toDate,
        int rangeMinutes)
    {
        if (rangeMinutes <= 0 || stops.Count == 0) return 0;

        var sum = 0.0;
        foreach (var stop in stops)
        {
            var overlapStart = stop.StartTime > fromDate ? stop.StartTime : fromDate;
            var overlapEnd = stop.EndTime < toDate ? stop.EndTime : toDate;
            if (overlapEnd <= overlapStart) continue;
            sum += (overlapEnd - overlapStart).TotalMinutes;
        }

        return Math.Min(rangeMinutes, Math.Max(0, (int)Math.Round(sum)));
    }

    public static TripAnalyticsSummaryDto BuildSummary(
        IReadOnlyList<GpsTripDto> trips,
        IReadOnlyList<TraccarSummary> summaries,
        IReadOnlyList<TraccarStop> stops,
        IReadOnlyList<TraccarEvent> events)
    {
        var tripCount = trips.Count;
        var tripDistanceKm = tripCount > 0 ? trips.Sum(t => t.DistanceKm) : 0;
        var tripDrivingMinutes = tripCount > 0 ? trips.Sum(t => t.DurationMinutes) : 0;

        // Empty Traccar summary rows (distance/engineHours all zero) must not wipe trip-list KPIs.
        var summaryDistanceKm = summaries.Count > 0 ? summaries.Sum(s => s.Distance / 1000.0) : 0;
        var summaryDrivingMinutes = summaries.Count > 0 ? summaries.Sum(s => ToDurationMinutes(s.EngineHours)) : 0;
        var useTripMotion = summaries.Count == 0
            || (summaryDistanceKm <= 0 && summaryDrivingMinutes <= 0 && tripDistanceKm > 0);

        var distanceKm = useTripMotion ? tripDistanceKm : summaryDistanceKm;
        var drivingMinutes = useTripMotion ? tripDrivingMinutes : summaryDrivingMinutes;

        var idleMinutes = stops.Sum(s =>
            StopDurationMinutes(s.StartTime, s.EndTime, s.Duration));

        double avgSpeed;
        double maxSpeed;
        if (useTripMotion)
        {
            avgSpeed = tripCount > 0 ? trips.Average(t => (double)t.AvgSpeedKmh) : 0;
            maxSpeed = tripCount > 0 ? trips.Max(t => (double)t.MaxSpeedKmh) : 0;
        }
        else
        {
            avgSpeed = summaries.Average(s => s.AverageSpeed * KnotsToKmh);
            maxSpeed = summaries.Max(s => s.MaxSpeed * KnotsToKmh);
        }

        var fuel = summaries.Sum(s => s.SpentFuel);
        decimal? fuelLiters = fuel > 0 ? fuel : null;

        // Traccar summaries: engine-on time from telemetry. Local trip rows: use summed trip duration.
        decimal? engineHours = drivingMinutes > 0
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
    /// History-range statistics: prefer odometer distance and motion-based moving time.
    /// IdleMinutes is the complementary non-moving residual of the selected window
    /// (Moving + Non-moving = window), so stop-duration sums cannot exceed the day.
    /// </summary>
    public static TripAnalyticsSummaryDto BuildHistoryStatistics(
        TripAnalyticsSummaryDto? baseSummary,
        IReadOnlyList<TripReplayPositionDto> route,
        IReadOnlyList<TripStopDto> stops,
        DateTime fromDate,
        DateTime toDate,
        double? mileageKm,
        decimal? traccarMaxSpeedKmh = null,
        decimal? traccarEngineHours = null)
    {
        var rangeMinutes = Math.Max(1, (int)Math.Ceiling((toDate - fromDate).TotalMinutes));
        var motionDriving = ComputeMotionDrivingMinutes(route, rangeMinutes);

        int drivingMinutes;
        if (motionDriving > 0)
        {
            drivingMinutes = motionDriving;
        }
        else if (baseSummary is not null && baseSummary.DrivingMinutes > 0 && baseSummary.DrivingMinutes <= rangeMinutes)
        {
            drivingMinutes = baseSummary.DrivingMinutes;
        }
        else
        {
            // Fallback when motion cannot be detected: residual after clipped stop time.
            var stopIdleFallback = ClippedIdleMinutes(stops, fromDate, toDate, rangeMinutes);
            drivingMinutes = Math.Max(0, rangeMinutes - stopIdleFallback);
            if (route.Count >= 2)
            {
                var first = route[0].Timestamp;
                var last = route[^1].Timestamp;
                var span = Math.Max(1, (int)Math.Ceiling((last - first).TotalMinutes));
                drivingMinutes = Math.Min(drivingMinutes, Math.Max(0, Math.Min(span, rangeMinutes)));
            }
        }

        drivingMinutes = Math.Clamp(drivingMinutes, 0, rangeMinutes);
        // Non-moving (IdleMinutes) is the complementary residual of the selected window.
        // This prevents Moving + Non-moving from exceeding 24h for a one-day range.
        var idleMinutes = Math.Max(0, rangeMinutes - drivingMinutes);

        var replaySummary = BuildReplaySummary(Array.Empty<TraccarSummary>(), route);
        var distanceKm = mileageKm
            ?? baseSummary?.DistanceKm
            ?? replaySummary?.DistanceKm
            ?? 0;

        // Traccar's summary sees every raw fix; the route may be downsampled.
        var routeMax = route.Count > 0 ? route.Max(p => p.SpeedKmh) : 0m;
        var maxSpeed = Math.Round(Math.Max(Math.Max(routeMax, traccarMaxSpeedKmh ?? 0m), baseSummary?.MaxSpeedKmh ?? 0m), 1);

        // Average speed is derived, not a mean of point readings, so the strip reconciles:
        // DISTANCE ÷ MOVING = AVG SPEED. Capped at max speed so odometer distance covered
        // during a skipped data gap cannot produce an impossible average.
        var avgSpeed = drivingMinutes > 0
            ? Math.Round((decimal)distanceKm / (drivingMinutes / 60m), 1)
            : 0m;
        if (maxSpeed > 0m && avgSpeed > maxSpeed)
            avgSpeed = maxSpeed;

        // Engine hours = ignition-on time, never moving time (idling with the engine running counts).
        var engineHours = traccarEngineHours is > 0m
            ? Math.Round(traccarEngineHours.Value, 1)
            : ComputeIgnitionOnMinutes(route, rangeMinutes) is { } ignitionMinutes
                ? Math.Round(ignitionMinutes / 60m, 1)
                : (decimal?)null;

        return new TripAnalyticsSummaryDto(
            baseSummary?.TripCount ?? 0,
            Math.Round(distanceKm, 2),
            drivingMinutes,
            idleMinutes,
            baseSummary?.FuelLiters,
            avgSpeed,
            maxSpeed,
            stops.Count,
            baseSummary?.OverspeedCount ?? 0,
            baseSummary?.HarshBrakeCount ?? 0,
            baseSummary?.HarshAccelCount ?? 0,
            engineHours);
    }

    /// <summary>Segments longer than this are never counted as moving (sparse histories).</summary>
    internal const double MaxMovingGapMinutes = 20;

    /// <summary>
    /// The single moving-segment rule shared by moving time and replay stop detection.
    /// </summary>
    internal static bool IsMovingSegment(TripReplayPositionDto prev, TripReplayPositionDto next)
    {
        var deltaMinutes = (next.Timestamp - prev.Timestamp).TotalMinutes;
        if (deltaMinutes <= 0) return false;
        // Ignore very large gaps so sparse histories don't inflate driving time.
        if (deltaMinutes > MaxMovingGapMinutes) return false;

        var segmentDistanceKm = HaversineKm(
            prev.Latitude, prev.Longitude,
            next.Latitude, next.Longitude);

        var movingBySpeed =
            prev.SpeedKmh >= 3m || next.SpeedKmh >= 3m;
        var movingByIgnition =
            (prev.Ignition == true || next.Ignition == true) &&
            (prev.SpeedKmh > 0m || next.SpeedKmh > 0m);
        var movingByDistance = segmentDistanceKm >= 0.03; // ~30m

        return movingBySpeed || movingByIgnition || movingByDistance;
    }

    private static int ComputeMotionDrivingMinutes(
        IReadOnlyList<TripReplayPositionDto> route,
        int rangeMinutes)
    {
        if (route.Count < 2) return 0;
        var movingMinutes = 0.0;

        for (var i = 1; i < route.Count; i++)
        {
            var prev = route[i - 1];
            var next = route[i];
            if (IsMovingSegment(prev, next))
            {
                movingMinutes += (next.Timestamp - prev.Timestamp).TotalMinutes;
            }
        }

        return Math.Clamp((int)Math.Round(movingMinutes), 0, rangeMinutes);
    }

    /// <summary>
    /// Minutes with ignition on, from the route. Null when no point carries ignition data,
    /// so the UI shows "—" instead of a made-up figure.
    /// </summary>
    private static int? ComputeIgnitionOnMinutes(
        IReadOnlyList<TripReplayPositionDto> route,
        int rangeMinutes)
    {
        if (route.Count < 2 || route.All(p => p.Ignition is null)) return null;
        var minutes = 0.0;

        for (var i = 1; i < route.Count; i++)
        {
            var prev = route[i - 1];
            var deltaMinutes = (route[i].Timestamp - prev.Timestamp).TotalMinutes;
            if (deltaMinutes <= 0 || deltaMinutes > MaxMovingGapMinutes) continue;
            if (prev.Ignition == true)
                minutes += deltaMinutes;
        }

        return Math.Clamp((int)Math.Round(minutes), 0, rangeMinutes);
    }

    private static int CountEvents(IReadOnlyList<TraccarEvent> events, params string[] types)
    {
        return events.Count(e =>
            types.Any(t => e.Type.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Merge GpsAlertEvents safety counts (canonical ERP event store) with Traccar-derived summary KPIs.
    /// </summary>
    public static TripAnalyticsSummaryDto WithSafetyAlertCounts(
        TripAnalyticsSummaryDto summary,
        int overspeedAlerts,
        int harshBrakeAlerts,
        int harshAccelAlerts)
    {
        return summary with
        {
            OverspeedCount = Math.Max(summary.OverspeedCount, overspeedAlerts),
            HarshBrakeCount = Math.Max(summary.HarshBrakeCount, harshBrakeAlerts),
            HarshAccelCount = Math.Max(summary.HarshAccelCount, harshAccelAlerts)
        };
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

    internal static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
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
