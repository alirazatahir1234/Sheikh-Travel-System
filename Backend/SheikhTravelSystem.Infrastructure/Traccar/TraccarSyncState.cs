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

    public TraccarSyncStatusDto Snapshot(bool connected)
    {
        lock (_lock)
        {
            return new TraccarSyncStatusDto(
                options.Value.Enabled,
                connected,
                _isRunning,
                _lastPositionSyncAt,
                _lastDeviceSyncAt,
                _lastEventSyncAt,
                _lastSyncCompletedAt,
                _lastError,
                options.Value.ResolvedPositionIntervalSeconds);
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
}
