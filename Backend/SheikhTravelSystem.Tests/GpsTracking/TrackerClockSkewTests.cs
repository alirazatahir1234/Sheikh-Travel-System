using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Infrastructure.Traccar;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// JIMI VG03 trackers (BWY 217, Nov 2026) reported fixTime 5h behind serverTime, so a 07:15 PKT
/// trip showed as 02:15 AM in playback. These tests pin the detection and correction rules.
/// </summary>
public class TrackerClockSkewDetectorTests
{
    private static readonly DateTime T0 = new(2026, 11, 24, 2, 0, 0, DateTimeKind.Utc);

    /// <summary>Fixes every <paramref name="everyMinutes"/>, each received <paramref name="lag"/> after its fix time.</summary>
    private static List<TrackerClockSample> Stream(int count, TimeSpan lag, int everyMinutes = 5, double jitterSeconds = 4)
        => Enumerable.Range(0, count)
            .Select(i =>
            {
                var server = T0.AddMinutes(i * everyMinutes);
                return new TrackerClockSample(server - lag - TimeSpan.FromSeconds(jitterSeconds), server);
            })
            .ToList();

    [Fact]
    public void Detects_five_hour_slow_clock()
        => TrackerClockSkewDetector.DetectSkewHours(Stream(6, TimeSpan.FromHours(5))).Should().Be(5);

    [Fact]
    public void Detects_tracker_running_three_hours_ahead()
        => TrackerClockSkewDetector.DetectSkewHours(Stream(6, TimeSpan.FromHours(-3))).Should().Be(-3);

    [Fact]
    public void Correct_clock_gives_zero()
        => TrackerClockSkewDetector.DetectSkewHours(Stream(10, TimeSpan.Zero)).Should().Be(0);

    [Fact]
    public void Too_few_samples_gives_zero()
        => TrackerClockSkewDetector.DetectSkewHours(Stream(2, TimeSpan.FromHours(5))).Should().Be(0);

    [Fact]
    public void Samples_bunched_in_a_few_minutes_give_zero()
        => TrackerClockSkewDetector.DetectSkewHours(Stream(5, TimeSpan.FromHours(5), everyMinutes: 1)).Should().Be(0);

    [Fact]
    public void Lag_not_near_a_whole_hour_gives_zero()
        => TrackerClockSkewDetector.DetectSkewHours(Stream(6, TimeSpan.FromMinutes(150))).Should().Be(0);

    [Fact]
    public void Backlog_upload_with_shrinking_lag_is_not_a_skew()
    {
        // Correct clock, tracker was out of coverage: lags shrink from 3h towards 2h over 15 minutes.
        var samples = Enumerable.Range(0, 6)
            .Select(i =>
            {
                var server = T0.AddMinutes(i * 3);
                return new TrackerClockSample(server - TimeSpan.FromMinutes(180 - i * 12), server);
            })
            .ToList();

        TrackerClockSkewDetector.DetectSkewHours(samples).Should().Be(0);
    }

    [Fact]
    public void Frozen_keepalive_fix_counts_once()
    {
        // Parked: the same fix time is reported repeatedly with later server times.
        var fix = T0;
        var samples = Enumerable.Range(0, 6)
            .Select(i => new TrackerClockSample(fix, T0.AddHours(5).AddMinutes(i * 10)))
            .ToList();

        TrackerClockSkewDetector.DetectSkewHours(samples).Should().Be(0);
    }

    [Fact]
    public void One_fresh_correct_fix_clears_the_skew()
    {
        var samples = Stream(8, TimeSpan.FromHours(5));
        samples.Add(new TrackerClockSample(T0.AddHours(2), T0.AddHours(2).AddSeconds(3)));

        TrackerClockSkewDetector.DetectSkewHours(samples).Should().Be(0);
    }

    [Theory]
    [InlineData(300.5, 5, true)]   // skewed row
    [InlineData(330, 5, true)]     // skewed + buffered
    [InlineData(0.1, 5, false)]    // recorded after the tracker was fixed
    [InlineData(0.1, 0, false)]
    [InlineData(-180, -3, true)]
    [InlineData(0.1, -3, false)]
    public void RowCarriesSkew_only_for_rows_with_the_error(double lagMinutes, int skew, bool expected)
    {
        var server = T0;
        TrackerClockSkewDetector.RowCarriesSkew(server.AddMinutes(-lagMinutes), server, skew).Should().Be(expected);
    }
}

public class TrackerClockSkewRegistryTests
{
    private static readonly DateTime T0 = new(2026, 11, 24, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Learns_skew_from_live_fixes_and_ignores_repeated_fix_times()
    {
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance);
        for (var i = 0; i < 4; i++)
        {
            var server = T0.AddMinutes(i * 5);
            registry.Observe(23, server.AddHours(-5), server);
            registry.Observe(23, server.AddHours(-5), server.AddSeconds(30)); // keepalive repeat
        }

        registry.IsKnown(23).Should().BeTrue();
        registry.GetSkewHours(23).Should().Be(5);
        registry.GetSkewHours(99).Should().Be(0);
        registry.IsKnown(99).Should().BeFalse();
    }

    [Fact]
    public void Single_healthy_fix_is_enough_to_know_a_device()
    {
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance);
        registry.Observe(7, T0, T0.AddSeconds(2));

        registry.IsKnown(7).Should().BeTrue();
        registry.GetSkewHours(7).Should().Be(0);
    }

    [Fact]
    public void Inconclusive_evidence_keeps_the_detected_skew()
    {
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance);
        for (var i = 0; i < 4; i++)
        {
            var server = T0.AddMinutes(i * 5);
            registry.Observe(23, server.AddHours(-5), server);
        }

        // A buffered upload arrives 8h late — not near the 5h skew, not a fresh fix either.
        for (var i = 0; i < 6; i++)
        {
            var server = T0.AddMinutes(20 + i);
            registry.Observe(23, server.AddHours(-8).AddMinutes(-i), server);
        }

        registry.GetSkewHours(23).Should().Be(5);
    }

    [Fact]
    public void Fresh_correct_fix_clears_a_detected_skew()
    {
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance);
        for (var i = 0; i < 4; i++)
        {
            var server = T0.AddMinutes(i * 5);
            registry.Observe(23, server.AddHours(-5), server);
        }

        registry.Observe(23, T0.AddMinutes(30), T0.AddMinutes(30).AddSeconds(2));

        registry.GetSkewHours(23).Should().Be(0);
    }

    [Fact]
    public void Bootstrap_slot_is_rate_limited_per_device()
    {
        var time = new ManualTimeProvider(T0);
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance, time);

        registry.TryBeginBootstrap(23).Should().BeTrue();
        registry.TryBeginBootstrap(23).Should().BeFalse();
        registry.TryBeginBootstrap(24).Should().BeTrue();

        time.Advance(TrackerClockSkewRegistry.BootstrapCooldown);
        registry.TryBeginBootstrap(23).Should().BeTrue();
    }
}

public class ClockSkewCorrectingTraccarClientTests
{
    private const int Device = 23;
    private static readonly DateTime Now = new(2026, 11, 24, 3, 0, 0, DateTimeKind.Utc); // 08:00 PKT

    private static TraccarPosition Position(DateTime fix, DateTime server, int deviceId = Device) =>
        new(0, deviceId, "gt06", server, fix, fix, false, true, 32.2, 74.6, 0, 20, 0, null, 0,
            new TraccarPositionAttributes(null, null, null, null, null, null, null));

    /// <summary>Registry that already knows the device has <paramref name="skew"/> hours of skew.</summary>
    private static TrackerClockSkewRegistry KnownRegistry(int skew)
    {
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance);
        for (var i = 0; i < 4; i++)
        {
            var server = Now.AddMinutes(-60 + i * 5);
            registry.Observe(Device, server.AddHours(-skew).AddSeconds(-3), server);
        }
        return registry;
    }

    private static ClockSkewCorrectingTraccarClient Client(Mock<ITraccarClient> inner, ITrackerClockSkewRegistry registry) =>
        new(inner.Object, registry, NullLogger<ClockSkewCorrectingTraccarClient>.Instance, new ManualTimeProvider(Now));

    [Fact]
    public async Task Route_window_is_shifted_back_and_rows_are_corrected()
    {
        var inner = new Mock<ITraccarClient>(MockBehavior.Strict);
        var from = new DateTime(2026, 11, 23, 19, 0, 0, DateTimeKind.Utc); // 24 Nov 00:00 PKT
        var to = from.AddDays(1);
        var realTrip = new DateTime(2026, 11, 24, 2, 15, 0, DateTimeKind.Utc); // 07:15 PKT
        inner.Setup(c => c.GetRouteAsync(Device, from.AddHours(-5), to.AddHours(-5), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Position(realTrip.AddHours(-5), realTrip.AddSeconds(5))]);

        var route = await Client(inner, KnownRegistry(5)).GetRouteAsync(Device, from, to);

        route.Should().ContainSingle();
        route[0].FixTime.Should().Be(realTrip);
        route[0].DeviceTime.Should().Be(realTrip);
    }

    [Fact]
    public async Task Row_recorded_after_the_tracker_was_fixed_is_left_alone()
    {
        var inner = new Mock<ITraccarClient>();
        var fresh = Position(Now.AddMinutes(-1), Now.AddMinutes(-1).AddSeconds(2));
        inner.Setup(c => c.GetRouteAsync(Device, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([fresh]);

        var route = await Client(inner, KnownRegistry(5)).GetRouteAsync(Device, Now.AddHours(-1), Now);

        route[0].Should().BeSameAs(fresh);
    }

    [Fact]
    public async Task Stops_and_trips_are_shifted_by_the_device_skew()
    {
        var inner = new Mock<ITraccarClient>();
        var start = new DateTime(2026, 11, 23, 21, 15, 0, DateTimeKind.Utc);
        inner.Setup(c => c.GetStopsAsync(Device, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TraccarStop(Device, "BWY 217", start, start.AddMinutes(20), 32.2, 74.6, 1_200_000, null)]);
        inner.Setup(c => c.GetTripsAsync(Device, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TraccarTrip(Device, "BWY 217", start, start.AddMinutes(15), 32.23, 74.66, 32.20, 74.64, 9000, 20, 40, 900_000)]);
        var client = Client(inner, KnownRegistry(5));

        var stops = await client.GetStopsAsync(Device, Now.AddDays(-1), Now);
        var trips = await client.GetTripsAsync(Device, Now.AddDays(-1), Now);

        stops[0].StartTime.Should().Be(start.AddHours(5));
        stops[0].EndTime.Should().Be(start.AddMinutes(20).AddHours(5));
        trips[0].StartTime.Should().Be(start.AddHours(5));
        trips[0].EndTime.Should().Be(start.AddMinutes(15).AddHours(5));
    }

    [Fact]
    public async Task Only_position_based_events_are_shifted()
    {
        var inner = new Mock<ITraccarClient>();
        var t = new DateTime(2026, 11, 23, 21, 15, 0, DateTimeKind.Utc);
        inner.Setup(c => c.GetEventsAsync(Device, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TraccarEvent(1, Device, 555, null, "ignitionOn", t),
                new TraccarEvent(2, Device, 0, null, "deviceOnline", t),
            ]);

        var events = await Client(inner, KnownRegistry(5)).GetEventsAsync(Device, Now.AddDays(-1), Now);

        events[0].EventTime.Should().Be(t.AddHours(5));
        events[1].EventTime.Should().Be(t);
    }

    [Fact]
    public async Task Healthy_device_is_passed_through_unchanged()
    {
        var inner = new Mock<ITraccarClient>();
        var from = Now.AddHours(-6);
        IReadOnlyList<TraccarPosition> rows = [Position(Now.AddMinutes(-3), Now.AddMinutes(-3).AddSeconds(2))];
        inner.Setup(c => c.GetRouteAsync(Device, from, Now, It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance);
        registry.Observe(Device, Now.AddMinutes(-1), Now.AddMinutes(-1).AddSeconds(2));

        var route = await Client(inner, registry).GetRouteAsync(Device, from, Now);

        route.Should().BeSameAs(rows);
    }

    [Fact]
    public async Task Live_positions_feed_the_registry_and_come_back_corrected()
    {
        var inner = new Mock<ITraccarClient>();
        var registry = KnownRegistry(5);
        var server = Now.AddSeconds(-10);
        inner.Setup(c => c.GetLivePositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Position(server.AddHours(-5), server)]);

        var live = await Client(inner, registry).GetLivePositionsAsync();

        live[0].FixTime.Should().Be(server);
        registry.GetSkewHours(Device).Should().Be(5);
    }

    [Fact]
    public async Task Cold_device_is_bootstrapped_once_from_recent_positions()
    {
        var inner = new Mock<ITraccarClient>();
        var registry = new TrackerClockSkewRegistry(NullLogger<TrackerClockSkewRegistry>.Instance);
        inner.Setup(c => c.GetLatestPositionByDeviceAsync(Device, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Position(Now.AddHours(-5).AddSeconds(-4), Now));
        inner.Setup(c => c.GetPositionsByDeviceAsync(Device, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 6)
                .Select(i => Position(Now.AddMinutes(-60 + i * 10).AddHours(-5).AddSeconds(-3), Now.AddMinutes(-60 + i * 10)))
                .ToList());
        inner.Setup(c => c.GetRouteAsync(Device, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var client = Client(inner, registry);

        await client.GetRouteAsync(Device, Now.AddDays(-1), Now);
        await client.GetRouteAsync(Device, Now.AddDays(-1), Now);

        registry.GetSkewHours(Device).Should().Be(5);
        inner.Verify(c => c.GetPositionsByDeviceAsync(Device, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(c => c.GetRouteAsync(Device, Now.AddDays(-1).AddHours(-5), Now.AddHours(-5), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

internal sealed class ManualTimeProvider(DateTime utcNow) : TimeProvider
{
    private DateTimeOffset _now = new(utcNow, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
