using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class ReplayStopDetectorTests
{
    private static readonly DateTime From = new(2026, 11, 19, 7, 33, 0, DateTimeKind.Utc);

    private static TripReplayPositionDto P(DateTime t, double lat, double lng, decimal speed, bool? ignition = false) =>
        new(t, lat, lng, speed, null, ignition, null, null, null, null);

    [Fact]
    public void MultiDayStanding_IsOneParkingStop()
    {
        // Parked in one spot for 3 days, heartbeat every 15 minutes.
        var route = new List<TripReplayPositionDto>();
        for (var t = From; t <= From.AddDays(3); t = t.AddMinutes(15))
            route.Add(P(t, 31.5, 74.3, 0m));

        var stops = ReplayStopDetector.Detect(route, From, From.AddDays(3));

        stops.Should().ContainSingle();
        stops[0].DurationMinutes.Should().Be(3 * 24 * 60);
        HistoryReplayMapper.SplitParkingStops(stops).Should().ContainSingle();
    }

    [Fact]
    public void StopBetweenDrives_IsDetected_WithItsDuration()
    {
        var route = new List<TripReplayPositionDto>
        {
            P(From, 31.50, 74.30, 50m),
            P(From.AddMinutes(10), 31.55, 74.35, 0m),
            P(From.AddMinutes(25), 31.55, 74.35, 0m),
            P(From.AddMinutes(40), 31.5501, 74.3501, 0m),
            P(From.AddMinutes(45), 31.60, 74.40, 50m),
            P(From.AddMinutes(55), 31.65, 74.45, 50m),
        };

        var stops = ReplayStopDetector.Detect(route, From, From.AddMinutes(55));

        stops.Should().ContainSingle();
        stops[0].StartTime.Should().Be(From.AddMinutes(10));
        stops[0].EndTime.Should().Be(From.AddMinutes(40));
        stops[0].DurationMinutes.Should().Be(30);
    }

    [Fact]
    public void ShortStop_UnderMinimum_IsIgnored()
    {
        var route = new List<TripReplayPositionDto>
        {
            P(From, 31.50, 74.30, 50m),
            P(From.AddMinutes(10), 31.55, 74.35, 0m),
            P(From.AddMinutes(13), 31.55, 74.35, 0m),
            P(From.AddMinutes(20), 31.60, 74.40, 50m),
        };

        ReplayStopDetector.Detect(route, From, From.AddMinutes(20)).Should().BeEmpty();
    }

    [Fact]
    public void LargeDisplacementAcrossDataGap_IsNotAStop()
    {
        // Two stationary-looking fixes an hour apart but ~70 km apart: the vehicle drove in the gap.
        var route = new List<TripReplayPositionDto>
        {
            P(From, 31.50, 74.30, 0m),
            P(From.AddHours(1), 32.00, 74.80, 0m),
        };

        ReplayStopDetector.Detect(route, From, From.AddHours(1)).Should().BeEmpty();
    }

    [Fact]
    public void TrailingStationaryFix_StopRunsToWindowEnd()
    {
        var to = From.AddHours(5);
        var route = new List<TripReplayPositionDto>
        {
            P(From, 31.50, 74.30, 50m),
            P(From.AddMinutes(10), 31.55, 74.35, 0m),
        };

        var stops = ReplayStopDetector.Detect(route, From, to);

        stops.Should().ContainSingle();
        stops[0].EndTime.Should().Be(to);
        stops[0].DurationMinutes.Should().Be(290);
    }

    [Fact]
    public void StopStartingBeforeWindow_IsClippedToWindow()
    {
        var route = new List<TripReplayPositionDto>
        {
            P(From.AddHours(-2), 31.5, 74.3, 0m),
            P(From.AddHours(1), 31.5, 74.3, 0m),
            P(From.AddHours(1).AddMinutes(10), 31.6, 74.4, 50m),
        };

        var stops = ReplayStopDetector.Detect(route, From, From.AddHours(2));

        stops.Should().ContainSingle();
        stops[0].StartTime.Should().Be(From);
        stops[0].DurationMinutes.Should().Be(60);
    }
}

public class GpsUtcDateTimeClampTests
{
    [Fact]
    public void ClampToNow_FutureTo_IsClampedToNow()
    {
        var now = new DateTime(2026, 11, 26, 7, 30, 0, DateTimeKind.Utc);
        GpsUtcDateTime.ClampToNow(now.AddHours(11.5), now).Should().Be(now);
    }

    [Fact]
    public void ClampToNow_PastTo_IsUnchanged()
    {
        var now = new DateTime(2026, 11, 26, 7, 30, 0, DateTimeKind.Utc);
        var past = now.AddDays(-1);
        GpsUtcDateTime.ClampToNow(past, now).Should().Be(past);
    }
}
