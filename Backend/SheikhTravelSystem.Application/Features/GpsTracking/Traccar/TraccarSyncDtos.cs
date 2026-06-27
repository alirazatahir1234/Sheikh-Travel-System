namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

public record TraccarSyncJobResult(
    string Job,
    int Processed,
    int Imported,
    int Updated,
    int Skipped,
    string? Error = null);

public record TraccarSyncRunResult(
    DateTime CompletedAt,
    IReadOnlyList<TraccarSyncJobResult> Jobs);

public record TraccarSyncStatusDto(
    bool Enabled,
    bool Connected,
    bool IsRunning,
    DateTime? LastPositionSyncAt,
    DateTime? LastDeviceSyncAt,
    DateTime? LastEventSyncAt,
    DateTime? LastSyncCompletedAt,
    string? LastError);
