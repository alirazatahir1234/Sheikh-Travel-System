using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface ITrackingRepository
{
    Task InsertLocationAsync(
        int vehicleId,
        int? driverId,
        int? bookingId,
        double latitude,
        double longitude,
        decimal speed,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackingDto>> GetLiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackingDto>> GetHistoryAsync(
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}
