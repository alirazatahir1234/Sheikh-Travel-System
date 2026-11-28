using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// The History replay names its route ends, stops and events from the address cache only, and queues
/// what the cache does not know, so the response never waits for the geocoder.
/// </summary>
public class TripReplayAddressEnricherCacheTests
{
    private static readonly DateTime T0 = new(2026, 11, 28, 6, 0, 0, DateTimeKind.Utc);
    private const double Known = 32.2626;
    private const double Unknown = 32.3000;

    private sealed class CacheOnly : IReverseGeocodingService
    {
        public int LiveLookups { get; private set; }

        public Task<ReverseGeocodeResult?> GetAddressAsync(
            double latitude, double longitude, bool forceRefresh = false, CancellationToken cancellationToken = default, bool allowGoogle = false)
        {
            LiveLookups++;
            return Task.FromResult<ReverseGeocodeResult?>(null);
        }

        public Task<ReverseGeocodeResult?> GetCachedAddressAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult(Math.Abs(latitude - Known) < 1e-6
                ? new ReverseGeocodeResult("7 Mall Road, Pasrur", FromCache: true, PrimaryAddress: "7 Mall Road, Pasrur")
                : null);
    }

    private sealed class Queue : IGpsAddressLookupQueue
    {
        public List<(double Latitude, double Longitude)> Queued { get; } = [];
        public void Enqueue(double latitude, double longitude) => Queued.Add((latitude, longitude));
    }

    private static TripReplayPositionDto Fix(int minute, double lat) =>
        new(T0.AddMinutes(minute), lat, 74.663, 0m, null, null, null, null, null, null);

    private static TripReplayBundleDto Bundle() => new(
        Route: [Fix(0, Known), Fix(5, 32.28), Fix(10, Unknown)],
        Playback: [Fix(0, Known), Fix(10, Unknown)],
        Stops: [new TripStopDto(T0, T0.AddMinutes(30), Known, 74.663, null, 30), new TripStopDto(T0, T0.AddMinutes(9), Unknown, 74.663, null, 9)],
        Events: [new TripEventDto(T0, "alarm", Known, 74.663, null, 0m), new TripEventDto(T0, "overspeed", null, null, null, 80m)],
        Summary: null);

    [Fact]
    public async Task KnownPlacesAreNamed_UnknownOnesQueued_NoLiveLookup()
    {
        var geocoder = new CacheOnly();
        var queue = new Queue();
        var tally = new AddressLookupTally();

        var result = await TripReplayAddressEnricher.EnrichFromCacheAsync(Bundle(), geocoder, queue, CancellationToken.None, tally: tally);

        result.Route[0].Address.Should().Be("7 Mall Road, Pasrur");
        result.Route[1].Address.Should().BeNull("only the ends of the route are named");
        result.Route[^1].Address.Should().BeNull("the cache does not know it yet");
        result.Playback[0].Address.Should().Be("7 Mall Road, Pasrur");
        result.Stops[0].Address.Should().Be("7 Mall Road, Pasrur");
        result.Stops[1].Address.Should().BeNull();
        result.Events[0].Address.Should().Be("7 Mall Road, Pasrur");
        result.Events[1].Address.Should().BeNull("an event without a position has nothing to name");

        queue.Queued.Should().OnlyContain(p => Math.Abs(p.Latitude - Unknown) < 1e-6);
        tally.Queued.Should().Be(queue.Queued.Count);
        tally.Named.Should().Be(4);
        geocoder.LiveLookups.Should().Be(0);
    }

    [Fact]
    public async Task StopsAlreadyNamed_AreLeftAsTheyAre()
    {
        var geocoder = new CacheOnly();
        var queue = new Queue();

        var result = await TripReplayAddressEnricher.EnrichFromCacheAsync(Bundle(), geocoder, queue, CancellationToken.None, includeStops: false);

        result.Stops.Should().OnlyContain(s => s.Address == null);
        queue.Queued.Should().HaveCount(2, "the route's end and the playback's end, not the unknown stop");
    }
}
