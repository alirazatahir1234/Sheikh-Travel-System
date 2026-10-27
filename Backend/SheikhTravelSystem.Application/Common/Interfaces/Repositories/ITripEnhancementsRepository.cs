using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Trips.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface ITripEnhancementsRepository
{
    Task<int> CloneTripAsync(int tripId, CancellationToken cancellationToken = default);

    Task<BulkTripImportResult> ImportFromRowsAsync(
        IReadOnlyList<BulkTripImportRowDto> rows,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default);

    Task<int> SaveTemplateAsync(UpsertTripTemplateDto dto, CancellationToken cancellationToken = default);

    Task SoftDeleteTemplateAsync(int templateId, CancellationToken cancellationToken = default);

    Task<int> SpawnFromTemplateAsync(int templateId, DateTime? occurrenceDate, CancellationToken cancellationToken = default);

    Task<int> SpawnDueRecurringTemplatesAsync(CancellationToken cancellationToken = default);

    Task<TripInsightsDto> GetInsightsAsync(int tripId, CancellationToken cancellationToken = default);

    Task<TripCostEstimateDto> EstimateCostAsync(TripCostEstimateRequestDto request, CancellationToken cancellationToken = default);

    Task RecordTimelineEventAsync(
        int tripId,
        string eventType,
        string title,
        string? detail,
        CancellationToken cancellationToken = default);

    Task<TripAssignmentConflictDto?> GetAssignmentConflictAsync(
        int tripId,
        int? driverId,
        int? vehicleId,
        CancellationToken cancellationToken = default);

    Task LogDelayedEtaSentAsync(int tripId, string channel, string message, CancellationToken cancellationToken = default);
}

public sealed record BulkTripImportResult(int Created, int Failed, IReadOnlyList<string> Errors);
