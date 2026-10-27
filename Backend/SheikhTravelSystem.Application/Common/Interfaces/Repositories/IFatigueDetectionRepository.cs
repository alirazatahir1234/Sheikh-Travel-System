using SheikhTravelSystem.Application.Features.FatigueDetection;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IFatigueDetectionRepository
{
    Task<FatigueTenantSettingsDto> GetOrCreateSettingsAsync(int tenantId, CancellationToken ct = default);

    Task UpsertSettingsAsync(int tenantId, UpsertFatigueTenantSettingsRequest body, CancellationToken ct = default);

    Task<int?> GetOpenEventIdForDriverAsync(int tenantId, int driverId, CancellationToken ct = default);

    Task<int> InsertEventAsync(FatigueEventInsertRow row, CancellationToken ct = default);

    Task TouchOpenEventAsync(int tenantId, int eventId, FatigueSignalMetrics metrics, CancellationToken ct = default);

    Task<IReadOnlyList<FatigueEventListItemDto>> ListEventsAsync(
        int tenantId, string? status, int limit, CancellationToken ct = default);

    Task<IReadOnlyList<FatigueEventListItemDto>> ListEventsPlatformAsync(
        int? tenantId, string? status, int limit, CancellationToken ct = default);

    Task<decimal?> GetDriverRiskScoreAsync(int tenantId, int driverId, CancellationToken ct = default);

    Task<int?> GetDriverUserIdAsync(int tenantId, int driverId, CancellationToken ct = default);

    Task ResolveEventAsync(int tenantId, int eventId, string resolvedBy, CancellationToken ct = default);

    Task AcknowledgeEventAsync(int tenantId, int driverId, int eventId, CancellationToken ct = default);

    Task<DriverFatigueStatusDto?> GetDriverFatigueStatusAsync(
        int tenantId, int driverId, int eventId, CancellationToken ct = default);

    Task<DriverFatigueActivePromptDto?> GetActivePromptForDriverAsync(int tenantId, int driverId, CancellationToken ct = default);

    Task<IReadOnlyList<FatigueEscalationCandidateRow>> ListEscalationCandidatesAsync(CancellationToken ct = default);

    Task AdvanceEscalationAsync(
        int eventId,
        int newStage,
        string status,
        DateTime? nextEscalationUtc,
        string? note,
        CancellationToken ct = default);

    Task<int?> GetPrimaryGpsDeviceIdForVehicleAsync(int tenantId, int vehicleId, CancellationToken ct = default);

    Task<(string FullName, string? Phone)?> GetDriverContactAsync(int tenantId, int driverId, CancellationToken ct = default);

    Task<(int? VehicleId, int? BookingId)> GetActiveTripContextAsync(int tenantId, int driverId, CancellationToken ct = default);
}
