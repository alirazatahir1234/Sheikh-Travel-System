using SheikhTravelSystem.Application.Features.SosDispatch;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface ISosDispatchRepository
{
    Task<IReadOnlyList<VerifiedTeamCandidateRow>> GetVerifiedTeamCandidatesAsync(
        int tenantId, int excludeDriverId, CancellationToken ct = default);

    Task<(decimal? SpeedKmh, string? RouteJson, string? RouteSummary)> GetLiveTelemetryAndRouteAsync(
        int tenantId, int driverId, int? vehicleId, CancellationToken ct = default);

    Task<int?> GetPrimaryTowingPartnerIdAsync(int tenantId, string regionCode, CancellationToken ct = default);

    Task<IReadOnlyList<SosRecoveryPartnerDto>> ListPartnersAsync(int tenantId, CancellationToken ct = default);

    Task<int> UpsertPartnerAsync(int tenantId, int? id, UpsertSosRecoveryPartnerRequest request, CancellationToken ct = default);

    Task SoftDeletePartnerAsync(int tenantId, int id, CancellationToken ct = default);

    Task<IReadOnlyList<SosEventListItemDto>> ListEventsAsync(
        int tenantId, string? status, int limit, CancellationToken ct = default);

    Task<SosEventListItemDto?> GetEventAsync(int tenantId, int id, CancellationToken ct = default);

    Task<DriverSosStatusDto?> GetDriverSosStatusAsync(
        int tenantId, int driverId, int sosId, CancellationToken ct = default);

    Task ResolveEventAsync(int tenantId, int id, string resolvedBy, CancellationToken ct = default);

    Task<int> InsertSosAlertWithDispatchAsync(
        int tenantId,
        int driverId,
        int? vehicleId,
        int? bookingId,
        double? lat,
        double? lng,
        string? message,
        SosDispatchPlan plan,
        DateTime createdAt,
        CancellationToken ct = default);
}
