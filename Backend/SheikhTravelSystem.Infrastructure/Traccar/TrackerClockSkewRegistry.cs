using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Traccar;

/// <summary>Per-Traccar-device clock-skew state, learned from live fixes. Process-wide singleton.</summary>
public interface ITrackerClockSkewRegistry
{
    /// <summary>Current whole-hour skew for the device (0 when healthy or unknown).</summary>
    int GetSkewHours(int traccarDeviceId);

    /// <summary>True when there is enough evidence to trust <see cref="GetSkewHours"/>.</summary>
    bool IsKnown(int traccarDeviceId);

    /// <summary>Record a raw (uncorrected) fix. Repeated fix times are ignored.</summary>
    void Observe(int traccarDeviceId, DateTime fixUtc, DateTime serverUtc);

    /// <summary>Claim a cold-start bootstrap slot; false when one ran for this device recently.</summary>
    bool TryBeginBootstrap(int traccarDeviceId);
}

public sealed class TrackerClockSkewRegistry : ITrackerClockSkewRegistry
{
    // Moving trackers report every few seconds, so the window is time-based (a 20-sample buffer
    // covered < 10 min and missed the skew ~45% of the time on real BWY 217 data; 6 h: 99.9%).
    internal static readonly TimeSpan SampleWindow = TimeSpan.FromHours(6);
    internal const int MaxSamplesPerDevice = 300;
    internal static readonly TimeSpan BootstrapCooldown = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<int, DeviceState> _devices = new();
    private readonly TimeProvider _time;
    private readonly ILogger<TrackerClockSkewRegistry> _logger;

    public TrackerClockSkewRegistry(ILogger<TrackerClockSkewRegistry> logger)
        : this(logger, TimeProvider.System)
    {
    }

    internal TrackerClockSkewRegistry(ILogger<TrackerClockSkewRegistry> logger, TimeProvider time)
    {
        _logger = logger;
        _time = time;
    }

    public int GetSkewHours(int traccarDeviceId) =>
        _devices.TryGetValue(traccarDeviceId, out var state) ? state.SkewHours : 0;

    public bool IsKnown(int traccarDeviceId)
    {
        if (!_devices.TryGetValue(traccarDeviceId, out var state))
            return false;

        lock (state.Sync)
        {
            return state.Decided;
        }
    }

    public void Observe(int traccarDeviceId, DateTime fixUtc, DateTime serverUtc)
    {
        var sample = new TrackerClockSample(fixUtc.ToUniversalTime(), serverUtc.ToUniversalTime());
        var state = _devices.GetOrAdd(traccarDeviceId, _ => new DeviceState());

        int previous;
        int current;
        lock (state.Sync)
        {
            if (state.Samples.Any(s => s.FixUtc == sample.FixUtc))
                return;

            state.Samples.Add(sample);
            var newest = state.Samples.Max(s => s.ServerUtc);
            state.Samples.RemoveAll(s => s.ServerUtc < newest - SampleWindow);
            if (state.Samples.Count > MaxSamplesPerDevice)
                state.Samples.RemoveRange(0, state.Samples.Count - MaxSamplesPerDevice);

            previous = state.SkewHours;
            // Inconclusive evidence keeps the previous decision (no flapping while parked or uploading a backlog).
            if (TrackerClockSkewDetector.Evaluate(state.Samples) is not { } decided)
                return;

            state.Decided = true;
            current = decided;
            state.SkewHours = current;
        }

        if (current == previous)
            return;

        if (current != 0)
        {
            _logger.LogWarning(
                "Tracker clock skew detected for Traccar device {TraccarDeviceId}: {SkewHours}h. GPS times are being corrected; fix the tracker or Traccar timezone setting",
                traccarDeviceId, current);
        }
        else
        {
            _logger.LogInformation(
                "Tracker clock skew cleared for Traccar device {TraccarDeviceId} (was {PreviousSkewHours}h)",
                traccarDeviceId, previous);
        }
    }

    public bool TryBeginBootstrap(int traccarDeviceId)
    {
        var state = _devices.GetOrAdd(traccarDeviceId, _ => new DeviceState());
        var now = _time.GetUtcNow();
        lock (state.Sync)
        {
            if (state.LastBootstrapAt is { } last && now - last < BootstrapCooldown)
                return false;
            state.LastBootstrapAt = now;
            return true;
        }
    }

    private sealed class DeviceState
    {
        public readonly object Sync = new();
        public readonly List<TrackerClockSample> Samples = [];
        public int SkewHours;
        public bool Decided;
        public DateTimeOffset? LastBootstrapAt;
    }
}
