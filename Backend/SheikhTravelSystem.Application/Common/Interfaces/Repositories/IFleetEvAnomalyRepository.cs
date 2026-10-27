using SheikhTravelSystem.Application.Features.Fleet.EvAnomaly;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IFleetEvAnomalyRepository
{
    Task<EvFleetDashboardDto> GetEvDashboardAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChargingStationDto>> ListChargingStationsAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<int> UpsertChargingStationAsync(
        int tenantId, UpsertChargingStationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FleetAnomalyEventDto>> ListAnomaliesAsync(
        int tenantId, bool unreviewedOnly, int? vehicleId, CancellationToken cancellationToken = default);

    Task<bool> ReviewAnomalyAsync(
        int tenantId, long anomalyId, string reviewedBy, string? notes, CancellationToken cancellationToken = default);

    Task<long> InsertAnomalyAsync(
        FleetAnomalyInsert insert, CancellationToken cancellationToken = default);

    Task<DriverEvStatusDto?> GetDriverEvStatusAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDocumentTrackerItemDto>> GetVehicleDocumentTrackerAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<int> UpsertComplianceDocumentAsync(
        int tenantId, UpsertComplianceDocumentRequest request, string? actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDocumentExpiryDto>> GetDriverVehicleDocumentExpiriesAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default);
}

public sealed record FleetAnomalyInsert(
    int TenantId,
    int VehicleId,
    int? DriverId,
    string AnomalyType,
    string Severity,
    double? Latitude,
    double? Longitude,
    decimal? SpeedKmh,
    string Details);
