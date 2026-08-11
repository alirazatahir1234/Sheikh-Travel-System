using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class TripAnalyticsMapperTests
{
    [Fact]
    public void ToStopDto_Duration60000Ms_IsOneMinute_WhenNoValidWallClock()
    {
        // End == Start so wall-clock is skipped; Duration 60_000 must be ms → 1 min (not /60 = 1000).
        var start = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);
        var stop = new TraccarStop(1, "dev", start, start, 31.5, 74.3, 60_000, null);

        var dto = TripAnalyticsMapper.ToStopDto(stop);

        Assert.Equal(1, dto.DurationMinutes);
    }

    [Fact]
    public void ToStopDto_PrefersWallClockOverDurationField()
    {
        var start = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(45);
        var stop = new TraccarStop(1, "dev", start, end, 31.5, 74.3, 999_999_999, null);

        var dto = TripAnalyticsMapper.ToStopDto(stop);

        Assert.Equal(45, dto.DurationMinutes);
    }

    [Fact]
    public void DurationRawToMinutes_ShortMsStop_NotInflatedAsSeconds()
    {
        Assert.Equal(1, TripAnalyticsMapper.DurationRawToMinutes(60_000));
        Assert.Equal(2, TripAnalyticsMapper.DurationRawToMinutes(120_000));
        // < 1000 → treat as seconds: Round(90/60) = 2
        Assert.Equal(2, TripAnalyticsMapper.DurationRawToMinutes(90));
    }

    [Fact]
    public void ClippedIdleMinutes_MultiDayStop_OnlyOverlapsWindow()
    {
        var windowFrom = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var windowTo = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc);
        var rangeMinutes = (int)Math.Ceiling((windowTo - windowFrom).TotalMinutes);

        // Parked from Aug 7 noon through Aug 9 noon — full stop is ~48h but overlap with Aug 8 is ~range.
        var stops = new List<TripStopDto>
        {
            new(
                windowFrom.AddDays(-1).AddHours(12),
                windowTo.AddDays(1).AddHours(-12),
                31.5,
                74.3,
                null,
                48 * 60)
        };

        var idle = TripAnalyticsMapper.ClippedIdleMinutes(stops, windowFrom, windowTo, rangeMinutes);

        Assert.True(idle <= rangeMinutes);
        Assert.InRange(idle, rangeMinutes - 2, rangeMinutes); // ~full day overlap
        // Must NOT equal the unclipped 48h duration
        Assert.NotEqual(48 * 60, idle);
    }

    [Fact]
    public void BuildHistoryStatistics_DrivingPlusIdle_DoesNotExceedRange()
    {
        var from = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1).AddMinutes(-1); // ~1439 min
        var rangeMinutes = Math.Max(1, (int)Math.Ceiling((to - from).TotalMinutes));

        // Large overlapping idle (would be full window when clipped)
        var stops = new List<TripStopDto>
        {
            new(from.AddHours(-6), to.AddHours(6), 31.5, 74.3, null, 60 * 48)
        };

        // Route with enough motion segments to claim ~6h driving
        var route = new List<TripReplayPositionDto>();
        var t = from.AddHours(8);
        for (var i = 0; i < 40; i++)
        {
            route.Add(new TripReplayPositionDto(
                t.AddMinutes(i * 10),
                31.50 + i * 0.001,
                74.30 + i * 0.001,
                SpeedKmh: 40m,
                Heading: 90,
                Ignition: true,
                Altitude: null,
                Address: null,
                BatteryLevel: null,
                Satellites: null));
        }

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(
            baseSummary: null,
            route,
            stops,
            from,
            to,
            mileageKm: 50);

        Assert.Equal(rangeMinutes, stats.DrivingMinutes + stats.IdleMinutes);
        Assert.True(stats.IdleMinutes <= rangeMinutes);
        Assert.True(stats.DrivingMinutes >= 0);
        Assert.True(stats.DrivingMinutes > 0); // motion should be detected
    }

    [Fact]
    public void BuildHistoryStatistics_AvgSpeed_IgnoresStationaryPoints()
    {
        var from = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddHours(2);
        var route = new List<TripReplayPositionDto>
        {
            new(from, 31.5, 74.3, 0m, null, false, null, null, null, null),
            new(from.AddMinutes(10), 31.5, 74.3, 0m, null, false, null, null, null, null),
            new(from.AddMinutes(20), 31.51, 74.31, 40m, null, true, null, null, null, null),
            new(from.AddMinutes(30), 31.52, 74.32, 60m, null, true, null, null, null, null),
        };

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(null, route, [], from, to, null);

        Assert.Equal(50.0m, stats.AvgSpeedKmh); // mean of 40 and 60 only
    }
}
