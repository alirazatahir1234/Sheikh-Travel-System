using FluentAssertions;
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
    public void BuildSummary_LocalTrips_ProvidesEngineHoursFromDuration()
    {
        var trips = new List<GpsTripDto>
        {
            new(1, "V1", 1, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
                12.5, 45m, 70m, 60, null, null, null, null, null, null, "Completed", null, null, null, null)
        };

        var summary = TripAnalyticsMapper.BuildSummary(trips, [], [], []);

        Assert.Equal(12.5, summary.DistanceKm);
        Assert.Equal(60, summary.DrivingMinutes);
        Assert.Equal(1.0m, summary.EngineHours);
    }

    [Fact]
    public void BuildSummary_ZeroDistanceTraccarSummaries_FallBackToTripMotion()
    {
        var trips = new List<GpsTripDto>
        {
            new(1, "V1", 1, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
                22.5, 15m, 72m, 90, null, null, null, null, null, null, "Completed", null, null, null, null)
        };
        var emptySummaries = new List<TraccarSummary>
        {
            new(1, "dev", 0, 0, 0, 0, 0)
        };

        var summary = TripAnalyticsMapper.BuildSummary(trips, emptySummaries, [], []);

        Assert.Equal(22.5, summary.DistanceKm);
        Assert.Equal(90, summary.DrivingMinutes);
        Assert.Equal(15.0m, summary.AvgSpeedKmh);
        Assert.Equal(72.0m, summary.MaxSpeedKmh);
        Assert.Equal(1.5m, summary.EngineHours);
    }

    [Fact]
    public void WithSafetyAlertCounts_MergesGpsAlertStore()
    {
        var baseSummary = new TripAnalyticsSummaryDto(1, 10, 30, 0, null, 40m, 60m, 0, 0, 0, 0, 0.5m);
        var merged = TripAnalyticsMapper.WithSafetyAlertCounts(baseSummary, overspeedAlerts: 4, harshBrakeAlerts: 1, harshAccelAlerts: 0);
        Assert.Equal(4, merged.OverspeedCount);
        Assert.Equal(1, merged.HarshBrakeCount);
    }

    [Fact]
    public void BuildHistoryStatistics_AvgSpeed_IsDistanceOverMovingTime()
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

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(null, route, [], from, to, mileageKm: 10);

        // Stationary first segment is not moving: moving = 20 min. 10 km / (20/60 h) = 30 km/h,
        // not the 50 km/h mean of the point readings.
        stats.DrivingMinutes.Should().Be(20);
        stats.AvgSpeedKmh.Should().Be(30.0m);
    }

    [Fact]
    public void BuildHistoryStatistics_AvgSpeed_NeverExceedsMaxSpeed()
    {
        var from = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var route = MovingRoute(from, segments: 1, speed: 40m, ignition: true);

        // Odometer distance covered during a data gap would otherwise give an impossible average.
        var stats = TripAnalyticsMapper.BuildHistoryStatistics(null, route, [], from, from.AddHours(2), mileageKm: 500);

        stats.AvgSpeedKmh.Should().Be(stats.MaxSpeedKmh);
    }

    [Fact]
    public void BuildHistoryStatistics_MovingPlusIdle_EqualsClampedWindow()
    {
        var utcNow = new DateTime(2026, 11, 26, 12, 30, 0, DateTimeKind.Utc);
        var from = utcNow.AddDays(-7);
        var requestedTo = new DateTime(2026, 11, 26, 18, 59, 0, DateTimeKind.Utc); // 23:59 PKT, in the future
        var effectiveTo = GpsUtcDateTime.ClampToNow(requestedTo, utcNow);
        var route = MovingRoute(from.AddHours(1), segments: 12, speed: 50m, ignition: true);

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(null, route, [], from, effectiveTo, mileageKm: 100);

        (stats.DrivingMinutes + stats.IdleMinutes).Should().Be((int)(utcNow - from).TotalMinutes);
        stats.DrivingMinutes.Should().Be(120);
    }

    [Fact]
    public void BuildHistoryStatistics_EngineHours_IsIgnitionOnTime_NotMovingTime()
    {
        var from = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var route = new List<TripReplayPositionDto>();
        // 50 min idling in place with the engine on…
        for (var i = 0; i <= 5; i++)
            route.Add(new(from.AddMinutes(i * 10), 31.5, 74.3, 0m, null, true, null, null, null, null));
        // …then 20 min driving.
        route.Add(new(from.AddMinutes(60), 31.51, 74.31, 40m, null, true, null, null, null, null));
        route.Add(new(from.AddMinutes(70), 31.52, 74.32, 40m, null, false, null, null, null, null));

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(null, route, [], from, from.AddHours(2), mileageKm: 10);

        stats.DrivingMinutes.Should().Be(20);
        stats.EngineHours.Should().Be(1.2m); // 70 min ignition-on
    }

    [Fact]
    public void BuildHistoryStatistics_EngineHours_NullWithoutIgnitionData()
    {
        var from = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var route = MovingRoute(from, segments: 3, speed: 40m, ignition: null);

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(null, route, [], from, from.AddHours(2), mileageKm: 10);

        stats.DrivingMinutes.Should().BeGreaterThan(0);
        stats.EngineHours.Should().BeNull();
    }

    [Fact]
    public void BuildHistoryStatistics_EngineHours_PrefersTraccarSummary()
    {
        var from = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var route = MovingRoute(from, segments: 3, speed: 40m, ignition: true);

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(
            null, route, [], from, from.AddHours(6), mileageKm: 10, traccarEngineHours: 3.4m);

        stats.EngineHours.Should().Be(3.4m);
    }

    [Fact]
    public void BuildHistoryStatistics_MaxSpeed_UsesTraccarSummaryWhenHigher()
    {
        var from = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        var route = MovingRoute(from, segments: 3, speed: 60m, ignition: true);

        var stats = TripAnalyticsMapper.BuildHistoryStatistics(
            null, route, [], from, from.AddHours(2), mileageKm: 10, traccarMaxSpeedKmh: 95m);

        stats.MaxSpeedKmh.Should().Be(95m);
    }

    private static List<TripReplayPositionDto> MovingRoute(DateTime start, int segments, decimal speed, bool? ignition)
    {
        var route = new List<TripReplayPositionDto>();
        for (var i = 0; i <= segments; i++)
            route.Add(new(start.AddMinutes(i * 10), 31.5 + i * 0.01, 74.3 + i * 0.01, speed, null, ignition, null, null, null, null));
        return route;
    }
}
