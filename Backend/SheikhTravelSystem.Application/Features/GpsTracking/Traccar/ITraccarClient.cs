namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

public interface ITraccarClient
{
    Task<TraccarServer?> GetServerAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TraccarDevice>> GetDevicesAsync(CancellationToken ct = default);
    Task<TraccarDevice?> GetDeviceByIdAsync(int deviceId, CancellationToken ct = default);
    Task<TraccarDevice?> GetDeviceByUniqueIdAsync(string uniqueId, CancellationToken ct = default);

    Task<TraccarClientResult<TraccarDevice>> CreateDeviceAsync(TraccarDevicePayload payload, CancellationToken ct = default);
    Task<TraccarClientResult<TraccarDevice>> UpdateDeviceAsync(TraccarUpdateDevicePayload payload, CancellationToken ct = default);
    Task<bool> DeleteDeviceAsync(int deviceId, CancellationToken ct = default);

    /// <summary>Legacy narrow create — prefer <see cref="CreateDeviceAsync"/> with full payload.</summary>
    Task<TraccarDevice?> CreateDeviceAsync(string name, string uniqueId, string? category = null, CancellationToken ct = default);

    /// <summary>Legacy narrow update — prefer <see cref="UpdateDeviceAsync"/> with full payload.</summary>
    Task<bool> UpdateDeviceAsync(int deviceId, string name, string uniqueId, bool disabled, CancellationToken ct = default);

    Task<IReadOnlyList<TraccarPosition>> GetLivePositionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TraccarPosition>> GetPositionsByDeviceAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default);

    Task<IReadOnlyList<TraccarGeofence>> GetGeofencesAsync(CancellationToken ct = default);
    Task<TraccarGeofence?> CreateGeofenceAsync(string name, string area, string? description = null, CancellationToken ct = default);
    Task<bool> UpdateGeofenceAsync(int geofenceId, string name, string area, CancellationToken ct = default);
    Task<bool> DeleteGeofenceAsync(int geofenceId, CancellationToken ct = default);

    Task<IReadOnlyList<TraccarTrip>> GetTripsAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<TraccarStop>> GetStopsAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<TraccarEvent>> GetEventsAsync(int deviceId, DateTime from, DateTime to, string? type = null, CancellationToken ct = default);
    Task<IReadOnlyList<TraccarSummary>> GetSummaryAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default);

    Task<bool> SendCommandAsync(int deviceId, string type, CancellationToken ct = default);
}
