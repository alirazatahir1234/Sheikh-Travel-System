using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IDriverPerformanceScoringRepository
{
    Task<DriverScoreWeightsDto> GetOrCreateWeightsAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<DriverScoreWeightsDto> UpsertWeightsAsync(
        int tenantId,
        UpsertDriverScoreWeightsRequest body,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task RecordScoreHistoryAsync(
        int tenantId,
        IReadOnlyList<DriverScoreDto> scores,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverScoreHistoryDto>> GetHistoryAsync(
        int tenantId,
        int driverId,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverScoreSupportingEventDto>> GetSupportingEventsAsync(
        int tenantId,
        int driverId,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Completed trips with planned end + on-time counts (15-min grace) per driver.</summary>
    Task<IReadOnlyDictionary<int, (int Completed, int OnTime)>> GetOnTimeCountsAsync(
        int tenantId,
        IReadOnlyList<int> driverIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}
