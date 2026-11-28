using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Infrastructure.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// The background lookups behind a History replay: one place at a time at the free geocoder's pace,
/// no repeats of a place that got no answer, and a pause (said once, without coordinates) when the
/// geocoder stops answering.
/// </summary>
public class GpsAddressLookupHostedServiceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Geocoder : IReverseGeocodingService
    {
        /// <summary>Places (rounded like the cache) that are already in GpsAddressCache.</summary>
        public HashSet<(double, double)> Cached { get; } = [];
        public bool Answers { get; set; } = true;
        public int LiveLookups { get; private set; }

        public Task<ReverseGeocodeResult?> GetAddressAsync(
            double latitude, double longitude, bool forceRefresh = false, CancellationToken cancellationToken = default, bool allowGoogle = false)
        {
            LiveLookups++;
            allowGoogle.Should().BeFalse("background lookups never use a paid provider");
            return Task.FromResult(Answers ? new ReverseGeocodeResult("1 Canal Road, Pasrur") : null);
        }

        public Task<ReverseGeocodeResult?> GetCachedAddressAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult(Cached.Contains((Math.Round(latitude, 4), Math.Round(longitude, 4)))
                ? new ReverseGeocodeResult("1 Canal Road, Pasrur", FromCache: true)
                : null);
    }

    private sealed class CapturingLogger : ILogger<GpsAddressLookupHostedService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static (GpsAddressLookupHostedService Service, Geocoder Geocoder, Clock Clock, CapturingLogger Log) Create(bool enabled = true)
    {
        var geocoder = new Geocoder();
        var provider = new ServiceCollection().AddSingleton<IReverseGeocodingService>(geocoder).BuildServiceProvider();
        var clock = new Clock();
        var log = new CapturingLogger();
        var service = new GpsAddressLookupHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new GeocodingOptions { Enabled = enabled }),
            log,
            clock);
        return (service, geocoder, clock, log);
    }

    private static async Task<TimeSpan> ProcessNextAsync(GpsAddressLookupHostedService service)
    {
        service.TryTakeNext(out var place).Should().BeTrue("a place should be waiting");
        return await service.LookUpAsync(place.Latitude, place.Longitude, CancellationToken.None);
    }

    [Fact]
    public void ThePlace_WithinTheCachesRounding_IsQueuedOnce()
    {
        var (service, _, _, _) = Create();

        service.Enqueue(32.26261, 74.66301);
        service.Enqueue(32.26259, 74.66299); // the same ~11 m cache cell
        service.Enqueue(32.27000, 74.66300);

        service.PendingCount.Should().Be(2);
    }

    [Fact]
    public async Task AnAnsweredPlace_CostsOneProviderCall_AndTheNextLookupWaitsItsTurn()
    {
        var (service, geocoder, _, _) = Create();
        service.Enqueue(32.2626, 74.6630);

        var wait = await ProcessNextAsync(service);

        geocoder.LiveLookups.Should().Be(1);
        wait.Should().Be(GpsAddressLookupHostedService.Spacing, "at most one provider call a second");
        service.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task APlaceNamedMeanwhile_CostsNoProviderCall_AndNoWait()
    {
        var (service, geocoder, _, _) = Create();
        geocoder.Cached.Add((32.2626, 74.663));
        service.Enqueue(32.2626, 74.6630);

        var wait = await ProcessNextAsync(service);

        geocoder.LiveLookups.Should().Be(0);
        wait.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task APlaceWithNoAnswer_IsNotAskedAgainFor30Minutes()
    {
        var (service, geocoder, clock, _) = Create();
        geocoder.Answers = false;
        service.Enqueue(32.2626, 74.6630);
        await ProcessNextAsync(service);

        service.Enqueue(32.2626, 74.6630);
        service.PendingCount.Should().Be(0, "it got no answer a moment ago");

        clock.Now += GpsAddressLookupHostedService.RetryAfter + TimeSpan.FromMinutes(1);
        service.Enqueue(32.2626, 74.6630);
        service.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task FiveUnansweredInARow_PauseFiveMinutes_SaidOnceWithoutCoordinates()
    {
        var (service, geocoder, _, log) = Create();
        geocoder.Answers = false;
        var waits = new List<TimeSpan>();
        for (var i = 0; i < GpsAddressLookupHostedService.FailuresBeforePause + 1; i++)
        {
            service.Enqueue(32.0 + i * 0.01, 74.6630);
            waits.Add(await ProcessNextAsync(service));
        }

        waits.Take(GpsAddressLookupHostedService.FailuresBeforePause - 1).Should().OnlyContain(w => w == GpsAddressLookupHostedService.Spacing);
        waits[GpsAddressLookupHostedService.FailuresBeforePause - 1].Should().Be(GpsAddressLookupHostedService.PauseFor);
        waits[^1].Should().Be(GpsAddressLookupHostedService.Spacing, "the count starts again after the pause");

        var warnings = log.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        warnings.Should().ContainSingle();
        warnings[0].Message.Should().Contain("5 times in a row").And.NotContain("32.").And.NotContain("74.", "coordinates are PII");
    }

    [Fact]
    public async Task AnAnswer_ResetsTheCountOfUnansweredLookups()
    {
        var (service, geocoder, _, log) = Create();
        var noAnswers = Enumerable.Repeat(false, GpsAddressLookupHostedService.FailuresBeforePause - 1);
        var answered = noAnswers.Append(true).Concat(noAnswers).ToList();
        for (var i = 0; i < answered.Count; i++)
        {
            geocoder.Answers = answered[i];
            service.Enqueue(32.0 + i * 0.01, 74.6630);
            (await ProcessNextAsync(service)).Should().Be(GpsAddressLookupHostedService.Spacing);
        }

        log.Entries.Should().NotContain(e => e.Level == LogLevel.Warning, "never five unanswered in a row");
    }

    [Fact]
    public void WithGeocodingOff_NothingIsQueued()
    {
        var (service, _, _, _) = Create(enabled: false);

        service.Enqueue(32.2626, 74.6630);

        service.PendingCount.Should().Be(0);
    }

    [Fact]
    public void AFullQueue_LeavesTheNewPlaceForALaterLoad()
    {
        var (service, _, _, _) = Create();
        for (var i = 0; i < GpsAddressLookupHostedService.Capacity + 1; i++)
            service.Enqueue(30.0 + i * 0.001, 74.0);

        service.PendingCount.Should().Be(GpsAddressLookupHostedService.Capacity);
    }
}
