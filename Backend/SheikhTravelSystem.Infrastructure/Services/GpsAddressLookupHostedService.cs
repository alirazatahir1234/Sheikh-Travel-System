using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Looks up, in the background, the places a request could not name from GpsAddressCache
/// (<see cref="IGpsAddressLookupQueue"/>), so a History load never waits for the free geocoder.
/// One place at a time, and at most one provider call a second (the free geocoder's limit) for the
/// whole API. The geocoder saves each answer in GpsAddressCache, so a later load shows the name.
/// A place that got no answer is not asked again for <see cref="RetryAfter"/>. After
/// <see cref="FailuresBeforePause"/> unanswered lookups in a row the service pauses for
/// <see cref="PauseFor"/> and says so once in the log, with counts only, never coordinates.
/// </summary>
public sealed class GpsAddressLookupHostedService : BackgroundService, IGpsAddressLookupQueue
{
    public const int Capacity = 500;
    public const int FailuresBeforePause = 5;
    public static readonly TimeSpan Spacing = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan PauseFor = TimeSpan.FromMinutes(5);
    private const int CoordinateDecimals = 4;
    private const int MaxRemembered = 5000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<GeocodingOptions> _options;
    private readonly ILogger<GpsAddressLookupHostedService> _logger;
    private readonly TimeProvider _time;
    private readonly Channel<(double Latitude, double Longitude)> _queue =
        Channel.CreateBounded<(double, double)>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    private readonly ConcurrentDictionary<(double, double), byte> _pending = new();
    private readonly ConcurrentDictionary<(double, double), DateTimeOffset> _notBefore = new();
    private int _failuresInARow;

    public GpsAddressLookupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<GeocodingOptions> options,
        ILogger<GpsAddressLookupHostedService> logger)
        : this(scopeFactory, options, logger, TimeProvider.System)
    {
    }

    internal GpsAddressLookupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<GeocodingOptions> options,
        ILogger<GpsAddressLookupHostedService> logger,
        TimeProvider time)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _time = time;
    }

    /// <summary>Places waiting for a lookup (tests and diagnostics).</summary>
    internal int PendingCount => _pending.Count;

    public void Enqueue(double latitude, double longitude)
    {
        if (!_options.Value.Enabled || !double.IsFinite(latitude) || !double.IsFinite(longitude))
            return;

        var key = Key(latitude, longitude);
        if (_notBefore.TryGetValue(key, out var notBefore) && _time.GetUtcNow() < notBefore)
            return;
        if (!_pending.TryAdd(key, 0))
            return;
        // Full queue: drop this place for now; a later load queues it again.
        if (!_queue.Writer.TryWrite((latitude, longitude)))
            _pending.TryRemove(key, out _);
    }

    internal bool TryTakeNext(out (double Latitude, double Longitude) place) => _queue.Reader.TryRead(out place);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var (latitude, longitude) in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                var wait = await LookUpAsync(latitude, longitude, stoppingToken);
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, _time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>Looks up one queued place. Returns how long to wait before the next lookup.</summary>
    internal async Task<TimeSpan> LookUpAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var key = Key(latitude, longitude);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var geocoder = scope.ServiceProvider.GetRequiredService<IReverseGeocodingService>();

            // Named in the meantime (another request, the live map): no provider call, no wait.
            if (await geocoder.GetCachedAddressAsync(latitude, longitude, cancellationToken) is not null)
                return TimeSpan.Zero;

            var result = await geocoder.GetAddressAsync(
                latitude,
                longitude,
                forceRefresh: false,
                cancellationToken,
                allowGoogle: false);

            if (result is null)
                return Unanswered(key);

            // Saved by the geocoder; even a district-only answer is shown from the cache from now on.
            _failuresInARow = 0;
            return Spacing;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background address lookup failed");
            return Unanswered(key);
        }
        finally
        {
            _pending.TryRemove(key, out _);
        }
    }

    private TimeSpan Unanswered((double, double) key)
    {
        Remember(key);
        if (++_failuresInARow < FailuresBeforePause)
            return Spacing;

        _logger.LogWarning(
            "Background address lookups got no answer {Failures} times in a row; pausing {PauseMinutes} min. The geocoder may be unreachable or refusing this server.",
            _failuresInARow,
            PauseFor.TotalMinutes);
        _failuresInARow = 0;
        return PauseFor;
    }

    private void Remember((double, double) key)
    {
        var now = _time.GetUtcNow();
        if (_notBefore.Count >= MaxRemembered)
        {
            foreach (var (k, until) in _notBefore)
                if (until <= now) _notBefore.TryRemove(k, out _);
        }
        _notBefore[key] = now + RetryAfter;
    }

    private static (double, double) Key(double latitude, double longitude) => (
        Math.Round(latitude, CoordinateDecimals, MidpointRounding.AwayFromZero),
        Math.Round(longitude, CoordinateDecimals, MidpointRounding.AwayFromZero));
}
