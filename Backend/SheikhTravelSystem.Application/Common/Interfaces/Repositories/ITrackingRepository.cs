using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface ITrackingRepository
{
    /// <summary>
    /// Inserts a location if the vehicle belongs to <paramref name="tenantId"/>.
    /// Returns false when the vehicle is missing or not owned by the tenant.
    /// </summary>
    Task<bool> InsertLocationAsync(
        int tenantId,
        int vehicleId,
        int? driverId,
        int? bookingId,
        double latitude,
        double longitude,
        decimal speed,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackingDto>> GetLiveAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackingDto>> GetHistoryAsync(
        int tenantId,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}
