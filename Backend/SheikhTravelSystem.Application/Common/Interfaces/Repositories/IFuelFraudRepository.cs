using SheikhTravelSystem.Application.Features.FuelFraud;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IFuelFraudRepository
{
    Task<int> InsertPumpOrderAsync(FuelPumpOrderInsertRow row, CancellationToken ct = default);

    Task<IReadOnlyList<FuelPumpOrderRow>> ListPendingForVehicleAsync(
        int vehicleId,
        int maxAgeMinutes,
        CancellationToken ct = default);

    Task CompleteVerificationAsync(FuelPumpOrderVerificationUpdate update, CancellationToken ct = default);

    Task<IReadOnlyList<FuelFraudEventListItemDto>> ListEventsAsync(
        int tenantId,
        string? status,
        int limit,
        CancellationToken ct = default);

    Task<IReadOnlyList<FuelFraudEventListItemDto>> ListEventsPlatformAsync(
        int? tenantId,
        string? status,
        int limit,
        CancellationToken ct = default);

    Task<bool> ResolveEventAsync(int tenantId, int id, string resolvedBy, CancellationToken ct = default);

    Task<(decimal? FuelLevel, decimal? TankCapacity, string? VehicleName, string? Registration)?>
        GetVehicleFuelContextAsync(int tenantId, int vehicleId, CancellationToken ct = default);
}

public record FuelPumpOrderInsertRow(
    int TenantId,
    int VehicleId,
    int DriverId,
    decimal OrderedLiters,
    decimal? FuelLevelAtOrderPercent,
    decimal? TankCapacityLiters,
    string? StationName,
    double? Latitude,
    double? Longitude);

public record FuelPumpOrderVerificationUpdate(
    int Id,
    string Status,
    decimal? FuelLevelAfterPercent,
    decimal? ActualIncreaseLiters,
    decimal? ShortfallLiters,
    int? GpsAlertEventId,
    DateTime VerifiedAtUtc);
