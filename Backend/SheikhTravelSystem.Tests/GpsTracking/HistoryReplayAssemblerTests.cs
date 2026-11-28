using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// A History replay of a busy week (the size of the one that took 135 s live): its stops are the
/// timeline's, Traccar's stops report is not asked for, and nothing waits for the geocoder.
/// </summary>
public class HistoryReplayAssemblerTests
{
    private static readonly DateTime From = new(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(7);
    private static readonly TimeZoneInfo Karachi = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");

    /// <summary>The address cache: knows the places south of this latitude.</summary>
    private const double KnownBelow = 32.15;

    private sealed class CacheOnly : IReverseGeocodingService
    {
        public int LiveLookups { get; private set; }

        public Task<ReverseGeocodeResult?> GetAddressAsync(
            double latitude, double longitude, bool forceRefresh = false, CancellationToken cancellationToken = default, bool allowGoogle = false)
        {
            LiveLookups++;
            return Task.FromResult<ReverseGeocodeResult?>(null);
        }

        public Task<ReverseGeocodeResult?> GetCachedAddressAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            if (latitude >= KnownBelow) return Task.FromResult<ReverseGeocodeResult?>(null);
            var name = $"{Math.Round(latitude * 1000)} Canal Road, Pasrur";
            return Task.FromResult<ReverseGeocodeResult?>(new ReverseGeocodeResult(name, FromCache: true, PrimaryAddress: name));
        }
    }

    private sealed class Queue(bool failFirst = false) : IGpsAddressLookupQueue
    {
        private bool _failNext = failFirst;
        public List<(double Latitude, double Longitude)> Queued { get; } = [];

        public void Enqueue(double latitude, double longitude)
        {
            if (_failNext)
            {
                _failNext = false;
                throw new InvalidOperationException("queue broke");
            }
            Queued.Add((latitude, longitude));
        }
    }

    private sealed class RecordingLog : ILogger
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }

    private static TraccarPosition Fix(int id, DateTime t, double lat, double lon, double knots, bool ignition, decimal odo) =>
        new(id, 23, "test", t, t, t, false, true, lat, lon, 200, knots, 90, null, 5,
            new TraccarPositionAttributes(ignition, null, null, null, knots > 0 ? 250m : 0m, odo, knots > 0));

    /// <summary>
    /// 88 drives of 10 minutes (a fix every 15 s), each followed by a stand (a fix every 97 s): every fourth
    /// stand lasts about 4 hours (parking), the others about an hour. 9,000-odd fixes, like BWY 217's week.
    /// </summary>
    private static (List<TraccarPosition> Positions, List<TraccarEvent> Events) BusyWeek()
    {
        var positions = new List<TraccarPosition>();
        var events = new List<TraccarEvent>();
        var rnd = new Random(7);
        var t = From;
        var id = 1;
        double lat = 32.0, lon = 74.60;
        var odo = 100_000m;
        for (var cycle = 0; cycle < 88 && t < To; cycle++)
        {
            for (var i = 0; i < 40; i++)
            {
                lat += cycle % 2 == 0 ? 0.0008 : -0.0007;
                lon += 0.0011;
                odo += 220;
                positions.Add(Fix(id++, t, lat, lon, 30, true, odo));
                t = t.AddSeconds(15);
            }
            var standFixes = cycle % 4 == 0 ? 150 : 40;
            for (var i = 0; i < standFixes && t < To; i++)
            {
                positions.Add(Fix(id++, t, lat + (rnd.NextDouble() - 0.5) * 0.00004, lon + (rnd.NextDouble() - 0.5) * 0.00004, 0, false, odo));
                t = t.AddSeconds(97);
            }
            if (cycle % 10 == 0)
                events.Add(new TraccarEvent(cycle, 23, id - 1, null, "alarm", t.AddSeconds(-97), lat, lon, Attributes: new TraccarEventAttributes("sos")));
        }
        return (positions, events);
    }

    private static Mock<ITraccarClient> Traccar(List<TraccarPosition> positions, List<TraccarEvent> events)
    {
        var traccar = new Mock<ITraccarClient>();
        traccar.Setup(c => c.GetRouteAsync(23, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(positions);
        traccar.Setup(c => c.GetEventsAsync(23, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(events);
        traccar.Setup(c => c.GetSummaryAsync(23, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TraccarSummary(23, "Test", 518_300, 20, 55, 143 * 3_600_000L, 0)]);
        traccar.Setup(c => c.GetStopsAsync(23, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return traccar;
    }

    private static async Task<(HistoryReplayBundleDto Replay, Mock<ITraccarClient> Traccar, CacheOnly Geocoder, Queue Queue, RecordingLog Log, ReplayTimings Timings, List<TripReplayPositionDto> FullRoute)>
        BuildAsync(bool queueFailsFirst = false)
    {
        var (positions, events) = BusyWeek();
        var traccar = Traccar(positions, events);
        var timings = new ReplayTimings();
        var fullRoute = new List<TripReplayPositionDto>();
        var bundle = await TripReplayLoader.LoadFromTraccarAsync(
            traccar.Object, 23, From, To, CancellationToken.None, null, 3000, 1000, false, fullRoute, includeStops: false, timings);

        var geocoder = new CacheOnly();
        var queue = new Queue(queueFailsFirst);
        var log = new RecordingLog();
        var replay = await HistoryReplayAssembler.AssembleAsync(
            new HistoryReplayInput(bundle, fullRoute, true, 8072, From, To, Karachi, To, null, 23, "BWY 217"),
            geocoder, queue, log, CancellationToken.None, timings);
        return (replay, traccar, geocoder, queue, log, timings, fullRoute);
    }

    [Fact]
    public async Task TheStops_AreTheTimelinesStandstills_AndEveryCountAgrees()
    {
        var (replay, traccar, _, _, _, _, fullRoute) = await BuildAsync();

        fullRoute.Count.Should().BeGreaterThan(8500, "a week about the size of BWY 217's (9,142 fixes)");
        traccar.Verify(c => c.GetStopsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never,
            "Traccar's stops report is not asked for");

        var timeline = replay.Timeline!;
        replay.Stops.Should().Equal(HistoryTimelineBuilder.ToStops(timeline));
        replay.Stops.Should().NotBeEmpty();
        var totals = timeline.Totals;
        replay.Statistics!.StopCount.Should().Be(totals.StopCount, "the timeline's StopCount is every standstill, parking included");
        replay.DisplaySummary!.Stops.Should().Be(totals.StopCount);
        replay.DisplaySummary!.Parking.Should().Be(totals.ParkingCount);
        replay.Parking!.Should().HaveCount(totals.ParkingCount).And.OnlyContain(s => s.DurationMinutes >= HistoryTimelineBuilder.ParkingMinutes);
        totals.ParkingCount.Should().BeGreaterThan(0);
        totals.StopCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NothingWaitsForTheGeocoder_AndUnknownPlacesAreQueued()
    {
        var (replay, _, geocoder, queue, _, timings, _) = await BuildAsync();

        geocoder.LiveLookups.Should().Be(0, "a replay names its places from the address cache only");
        replay.Stops.Where(s => s.Latitude < KnownBelow).Should().OnlyContain(s => s.Address != null && s.Address.EndsWith("Canal Road, Pasrur"));
        replay.Stops.Where(s => s.Latitude >= KnownBelow).Should().OnlyContain(s => s.Address == null, "named once the background lookup has run");
        queue.Queued.Should().NotBeEmpty().And.OnlyContain(p => p.Latitude >= KnownBelow);
        replay.Events.Where(e => e.Latitude < KnownBelow).Should().OnlyContain(e => e.Address != null);
        timings.Addresses.Queued.Should().Be(queue.Queued.Count);
        timings.Addresses.Named.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TheTimings_AreFilled_AndTheStopsReportIsMarkedNotAsked()
    {
        var (_, _, _, _, _, timings, _) = await BuildAsync();

        timings.StopsMs.Should().Be(-1);
        timings.RouteMs.Should().BeGreaterThanOrEqualTo(0);
        timings.TimelineMs.Should().BeGreaterThanOrEqualTo(0);
        timings.AddressesMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ATimelineThatCannotBeBuilt_LeavesTheReplay_WithTheDetectorsStops()
    {
        var (replay, _, _, _, log, _, fullRoute) = await BuildAsync(queueFailsFirst: true);

        replay.Timeline.Should().BeNull("the History page then shows the legacy strip");
        log.Levels.Should().Contain(LogLevel.Error);
        replay.Stops.Select(s => (s.StartTime, s.DurationMinutes)).Should().Equal(
            ReplayStopDetector.Detect(fullRoute, From, To).Select(s => (s.StartTime, s.DurationMinutes)));
        replay.Stops.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TheSingleTripReplay_StillAsksForTraccarsStops()
    {
        var (positions, events) = BusyWeek();
        var traccar = Traccar(positions, events);
        var timings = new ReplayTimings();

        await TripReplayLoader.LoadFromTraccarAsync(traccar.Object, 23, From, To, CancellationToken.None, timings: timings);

        traccar.Verify(c => c.GetStopsAsync(23, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        timings.StopsMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
