using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Traccar;

/// <summary>
/// Corrects GPS times from trackers whose clock is a whole number of hours off (see
/// <see cref="TrackerClockSkewDetector"/>). Every Traccar read in the app goes through
/// <see cref="ITraccarClient"/>, so playback, history, trips, stops, reports and ingest all
/// receive corrected times. Devices with no detected skew are passed through unchanged.
/// </summary>
public sealed class ClockSkewCorrectingTraccarClient : ITraccarClient
{
    internal static readonly TimeSpan BootstrapLookback = TimeSpan.FromHours(TrackerClockSkewDetector.MaxSkewHours + 1);
    internal static readonly TimeSpan BootstrapLookahead = TimeSpan.FromHours(TrackerClockSkewDetector.MaxSkewHours + 1);
    internal static readonly TimeSpan BootstrapRecentServerWindow = TimeSpan.FromHours(6);

    private readonly ITraccarClient _inner;
    private readonly ITrackerClockSkewRegistry _registry;
    private readonly ILogger<ClockSkewCorrectingTraccarClient> _logger;
    private readonly TimeProvider _time;

    public ClockSkewCorrectingTraccarClient(
        ITraccarClient inner,
        ITrackerClockSkewRegistry registry,
        ILogger<ClockSkewCorrectingTraccarClient> logger)
        : this(inner, registry, logger, TimeProvider.System)
    {
    }

    internal ClockSkewCorrectingTraccarClient(
        ITraccarClient inner,
        ITrackerClockSkewRegistry registry,
        ILogger<ClockSkewCorrectingTraccarClient> logger,
        TimeProvider time)
    {
        _inner = inner;
        _registry = registry;
        _logger = logger;
        _time = time;
    }

    // ── Positions ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TraccarPosition>> GetLivePositionsAsync(CancellationToken ct = default)
    {
        var positions = await _inner.GetLivePositionsAsync(ct);
        foreach (var p in positions)
            _registry.Observe(p.DeviceId, p.FixTime, p.ServerTime);

        var result = new List<TraccarPosition>(positions.Count);
        foreach (var p in positions)
        {
            var skew = await ResolveSkewAsync(p.DeviceId, ct);
            result.Add(CorrectPosition(p, skew));
        }
        return result;
    }

    public async Task<TraccarPosition?> GetLatestPositionByDeviceAsync(int deviceId, CancellationToken ct = default)
    {
        var position = await _inner.GetLatestPositionByDeviceAsync(deviceId, ct);
        if (position is null)
            return null;

        _registry.Observe(position.DeviceId, position.FixTime, position.ServerTime);
        var skew = await ResolveSkewAsync(position.DeviceId, ct);
        return CorrectPosition(position, skew);
    }

    public async Task<IReadOnlyList<TraccarPosition>> GetPositionsByDeviceAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var skew = await ResolveSkewAsync(deviceId, ct);
        if (skew == 0)
            return await _inner.GetPositionsByDeviceAsync(deviceId, from, to, ct);

        var raw = await _inner.GetPositionsByDeviceAsync(deviceId, from.AddHours(-skew), to.AddHours(-skew), ct);
        return raw.Select(p => CorrectPosition(p, skew)).ToList();
    }

    public async Task<IReadOnlyList<TraccarPosition>> GetRouteAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var skew = await ResolveSkewAsync(deviceId, ct);
        if (skew == 0)
            return await _inner.GetRouteAsync(deviceId, from, to, ct);

        var raw = await _inner.GetRouteAsync(deviceId, from.AddHours(-skew), to.AddHours(-skew), ct);
        return raw.Select(p => CorrectPosition(p, skew)).ToList();
    }

    // ── Reports (no serverTime on the rows — use the device's current skew) ──

    public async Task<IReadOnlyList<TraccarTrip>> GetTripsAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var skew = await ResolveSkewAsync(deviceId, ct);
        if (skew == 0)
            return await _inner.GetTripsAsync(deviceId, from, to, ct);

        var raw = await _inner.GetTripsAsync(deviceId, from.AddHours(-skew), to.AddHours(-skew), ct);
        return raw.Select(t => t with { StartTime = t.StartTime.AddHours(skew), EndTime = t.EndTime.AddHours(skew) }).ToList();
    }

    public async Task<IReadOnlyList<TraccarStop>> GetStopsAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var skew = await ResolveSkewAsync(deviceId, ct);
        if (skew == 0)
            return await _inner.GetStopsAsync(deviceId, from, to, ct);

        var raw = await _inner.GetStopsAsync(deviceId, from.AddHours(-skew), to.AddHours(-skew), ct);
        return raw.Select(s => s with { StartTime = s.StartTime.AddHours(skew), EndTime = s.EndTime.AddHours(skew) }).ToList();
    }

    public async Task<IReadOnlyList<TraccarEvent>> GetEventsAsync(int deviceId, DateTime from, DateTime to, string? type = null, CancellationToken ct = default)
    {
        var skew = await ResolveSkewAsync(deviceId, ct);
        if (skew == 0)
            return await _inner.GetEventsAsync(deviceId, from, to, type, ct);

        var raw = await _inner.GetEventsAsync(deviceId, from.AddHours(-skew), to.AddHours(-skew), type, ct);
        // Position-based events carry the fix time; online/offline events (no position) carry server time.
        return raw.Select(e => e.PositionId is > 0 ? e with { EventTime = e.EventTime.AddHours(skew) } : e).ToList();
    }

    public async Task<IReadOnlyList<TraccarSummary>> GetSummaryAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var skew = await ResolveSkewAsync(deviceId, ct);
        return skew == 0
            ? await _inner.GetSummaryAsync(deviceId, from, to, ct)
            : await _inner.GetSummaryAsync(deviceId, from.AddHours(-skew), to.AddHours(-skew), ct);
    }

    // ── Pass-through ─────────────────────────────────────────────────────────

    public Task<TraccarServer?> GetServerAsync(CancellationToken ct = default) => _inner.GetServerAsync(ct);
    public Task<IReadOnlyList<TraccarDevice>> GetDevicesAsync(CancellationToken ct = default) => _inner.GetDevicesAsync(ct);
    public Task<TraccarDevice?> GetDeviceByIdAsync(int deviceId, CancellationToken ct = default) => _inner.GetDeviceByIdAsync(deviceId, ct);
    public Task<TraccarDevice?> GetDeviceByUniqueIdAsync(string uniqueId, CancellationToken ct = default) => _inner.GetDeviceByUniqueIdAsync(uniqueId, ct);
    public Task<TraccarClientResult<TraccarDevice>> CreateDeviceAsync(TraccarDevicePayload payload, CancellationToken ct = default) => _inner.CreateDeviceAsync(payload, ct);
    public Task<TraccarClientResult<TraccarDevice>> UpdateDeviceAsync(TraccarUpdateDevicePayload payload, CancellationToken ct = default) => _inner.UpdateDeviceAsync(payload, ct);
    public Task<bool> DeleteDeviceAsync(int deviceId, CancellationToken ct = default) => _inner.DeleteDeviceAsync(deviceId, ct);
    public Task<TraccarDevice?> CreateDeviceAsync(string name, string uniqueId, string? category = null, CancellationToken ct = default) => _inner.CreateDeviceAsync(name, uniqueId, category, ct);
    public Task<bool> UpdateDeviceAsync(int deviceId, string name, string uniqueId, bool disabled, CancellationToken ct = default) => _inner.UpdateDeviceAsync(deviceId, name, uniqueId, disabled, ct);
    public Task<IReadOnlyList<TraccarGeofence>> GetGeofencesAsync(CancellationToken ct = default) => _inner.GetGeofencesAsync(ct);
    public Task<TraccarGeofence?> CreateGeofenceAsync(string name, string area, string? description = null, CancellationToken ct = default) => _inner.CreateGeofenceAsync(name, area, description, ct);
    public Task<bool> UpdateGeofenceAsync(int geofenceId, string name, string area, CancellationToken ct = default) => _inner.UpdateGeofenceAsync(geofenceId, name, area, ct);
    public Task<bool> DeleteGeofenceAsync(int geofenceId, CancellationToken ct = default) => _inner.DeleteGeofenceAsync(geofenceId, ct);
    public Task<bool> SendCommandAsync(int deviceId, string type, IDictionary<string, object>? attributes = null, CancellationToken ct = default) => _inner.SendCommandAsync(deviceId, type, attributes, ct);
    public Task<IReadOnlyList<string>> GetSupportedCommandTypesAsync(int deviceId, CancellationToken ct = default) => _inner.GetSupportedCommandTypesAsync(deviceId, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    internal static TraccarPosition CorrectPosition(TraccarPosition p, int skewHours)
    {
        if (skewHours == 0 || !TrackerClockSkewDetector.RowCarriesSkew(p.FixTime, p.ServerTime, skewHours))
            return p;

        var deviceTime = TrackerClockSkewDetector.RowCarriesSkew(p.DeviceTime, p.ServerTime, skewHours)
            ? p.DeviceTime.AddHours(skewHours)
            : p.DeviceTime;
        return p with { FixTime = p.FixTime.AddHours(skewHours), DeviceTime = deviceTime };
    }

    /// <summary>
    /// Skew for the device; after a restart, learns it once from recent raw positions
    /// (rate-limited per device) so the first minutes of ingest are not stored uncorrected.
    /// </summary>
    private async Task<int> ResolveSkewAsync(int deviceId, CancellationToken ct)
    {
        if (_registry.IsKnown(deviceId) || !_registry.TryBeginBootstrap(deviceId))
            return _registry.GetSkewHours(deviceId);

        try
        {
            var latest = await _inner.GetLatestPositionByDeviceAsync(deviceId, ct);
            if (latest is null)
                return _registry.GetSkewHours(deviceId);

            _registry.Observe(deviceId, latest.FixTime, latest.ServerTime);
            if (_registry.IsKnown(deviceId))
                return _registry.GetSkewHours(deviceId);

            var now = _time.GetUtcNow().UtcDateTime;
            var recent = await _inner.GetPositionsByDeviceAsync(
                deviceId, now - BootstrapLookback, now + BootstrapLookahead, ct);
            var cutoff = now - BootstrapRecentServerWindow;
            foreach (var p in recent
                         .Where(p => p.DeviceId == deviceId && p.ServerTime.ToUniversalTime() >= cutoff)
                         .OrderBy(p => p.ServerTime))
            {
                _registry.Observe(deviceId, p.FixTime, p.ServerTime);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Clock-skew bootstrap failed for Traccar device {TraccarDeviceId}", deviceId);
        }

        return _registry.GetSkewHours(deviceId);
    }
}
