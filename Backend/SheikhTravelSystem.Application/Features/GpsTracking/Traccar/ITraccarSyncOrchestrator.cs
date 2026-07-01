namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

public interface ITraccarSyncOrchestrator
{
    Task<TraccarSyncRunResult> SyncDevicesAsync(CancellationToken ct = default);
    Task<TraccarSyncRunResult> SyncPositionsAsync(CancellationToken ct = default);
    Task<TraccarSyncRunResult> SyncEventsAsync(CancellationToken ct = default);
    Task<TraccarSyncRunResult> RunManualSyncAsync(CancellationToken ct = default);
    Task<TraccarSyncRunResult> SyncTrackerAsync(int gpsDeviceId, CancellationToken ct = default);
}
