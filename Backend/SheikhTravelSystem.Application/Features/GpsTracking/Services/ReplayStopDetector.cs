using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Point-based stop detection for history replay, used when Traccar's stops report
/// returns nothing (sparse fixes, or the local-history fallback). Uses the same
/// moving-segment rule as moving time, so STOPS can never read 0 while the strip
/// reports days of standing.
/// </summary>
public static class ReplayStopDetector
{
    public const int DefaultMinStopMinutes = 5;
    public const double DefaultRadiusMeters = 200;

    /// <summary>
    /// A stop is a run of consecutive non-moving segments whose fixes stay within
    /// <paramref name="radiusMeters"/> of the run's first fix. A large displacement across
    /// a data gap breaks the run (unknown, not a stop). If the last fix is stationary the
    /// stop continues to <paramref name="toDate"/> — the vehicle is still standing.
    /// Durations are clipped to [fromDate, toDate].
    /// </summary>
    public static IReadOnlyList<TripStopDto> Detect(
        IReadOnlyList<TripReplayPositionDto> route,
        DateTime fromDate,
        DateTime toDate,
        int minStopMinutes = DefaultMinStopMinutes,
        double radiusMeters = DefaultRadiusMeters)
    {
        var stops = new List<TripStopDto>();
        if (route.Count == 0 || toDate <= fromDate) return stops;

        var i = 0;
        while (i < route.Count)
        {
            var anchor = route[i];
            if (anchor.SpeedKmh >= HistoryReplayMapper.MovingSpeedThresholdKmh)
            {
                i++;
                continue;
            }

            var j = i;
            while (j + 1 < route.Count)
            {
                var next = route[j + 1];
                if (TripAnalyticsMapper.IsMovingSegment(route[j], next)) break;
                if (next.SpeedKmh >= HistoryReplayMapper.MovingSpeedThresholdKmh) break;
                var driftMeters = TripAnalyticsMapper.HaversineKm(
                    anchor.Latitude, anchor.Longitude, next.Latitude, next.Longitude) * 1000;
                if (driftMeters > radiusMeters) break;
                j++;
            }

            var start = anchor.Timestamp;
            var end = route[j].Timestamp;
            if (j == route.Count - 1 && toDate > end)
                end = toDate;

            if (start < fromDate) start = fromDate;
            if (end > toDate) end = toDate;

            var durationMinutes = (int)Math.Round((end - start).TotalMinutes);
            if (end > start && durationMinutes >= minStopMinutes)
            {
                stops.Add(new TripStopDto(
                    start,
                    end,
                    anchor.Latitude,
                    anchor.Longitude,
                    anchor.Address,
                    durationMinutes));
            }

            i = j + 1;
        }

        return stops;
    }
}
