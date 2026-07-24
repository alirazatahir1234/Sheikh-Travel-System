using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Traccar;

public sealed class TraccarSyncState(IOptions<TraccarOptions> options) : ITraccarSyncState
{
    private readonly object _lock = new();
    private bool _isRunning;
    private DateTime? _lastPositionSyncAt;
    private DateTime? _lastDeviceSyncAt;
    private DateTime? _lastEventSyncAt;
    private DateTime? _lastSyncCompletedAt;
    private string? _lastError;
    private int _effectivePositionIntervalSeconds;
    private string _adaptiveReason = TraccarAdaptiveInterval.ReasonDefault;

    public TraccarSyncStatusDto Snapshot(bool connected)
    {
        lock (_lock)
        {
            var floor = Math.Max(1, options.Value.ResolvedPositionIntervalSeconds);
            var effective = _effectivePositionIntervalSeconds > 0
                ? _effectivePositionIntervalSeconds
                : floor;

            return new TraccarSyncStatusDto(
                options.Value.Enabled,
                connected,
                _isRunning,
                _lastPositionSyncAt,
                _lastDeviceSyncAt,
                _lastEventSyncAt,
                _lastSyncCompletedAt,
                _lastError,
                floor,
                options.Value.AdaptivePositionSync,
                _adaptiveReason,
                effective);
        }
    }

    public void MarkRunning(bool running)
    {
        lock (_lock)
        {
            _isRunning = running;
        }
    }

    public void RecordJobComplete(string job, TraccarSyncJobResult result)
    {
        lock (_lock)
        {
            var at = DateTime.UtcNow;
            if (result.Error is not null)
                _lastError = result.Error;

            switch (job)
            {
                case "positions":
                    _lastPositionSyncAt = at;
                    break;
                case "devices":
                    _lastDeviceSyncAt = at;
                    break;
                case "events":
                    _lastEventSyncAt = at;
                    break;
            }

            _lastSyncCompletedAt = at;
        }
    }

    public void RecordError(string? error)
    {
        lock (_lock)
        {
            _lastError = error;
        }
    }

    public void SetAdaptivePositionInterval(int intervalSeconds, string reason)
    {
        lock (_lock)
        {
            _effectivePositionIntervalSeconds = Math.Max(1, intervalSeconds);
            _adaptiveReason = string.IsNullOrWhiteSpace(reason)
                ? TraccarAdaptiveInterval.ReasonDefault
                : reason;
        }
    }

    public int GetEffectivePositionIntervalSeconds()
    {
        lock (_lock)
        {
            if (_effectivePositionIntervalSeconds > 0)
                return _effectivePositionIntervalSeconds;
            return Math.Max(1, options.Value.ResolvedPositionIntervalSeconds);
        }
    }
}
