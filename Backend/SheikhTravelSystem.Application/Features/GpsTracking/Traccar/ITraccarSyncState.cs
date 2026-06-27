namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

/// <summary>In-memory sync health for admin UI and scheduler.</summary>
public interface ITraccarSyncState
{
    TraccarSyncStatusDto Snapshot(bool connected);
    void MarkRunning(bool running);
    void RecordJobComplete(string job, TraccarSyncJobResult result);
    void RecordError(string? error);
}
