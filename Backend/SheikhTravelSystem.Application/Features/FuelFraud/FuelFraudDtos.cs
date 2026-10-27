namespace SheikhTravelSystem.Application.Features.FuelFraud;

public record FuelFraudEventListItemDto(
    int Id,
    int TenantId,
    string? TenantName,
    int VehicleId,
    string VehicleName,
    string? VehicleRegistration,
    int DriverId,
    string DriverName,
    decimal OrderedLiters,
    decimal? ActualIncreaseLiters,
    decimal? ShortfallLiters,
    decimal? FuelLevelAtOrderPercent,
    decimal? FuelLevelAfterPercent,
    string? StationName,
    double? Latitude,
    double? Longitude,
    string Status,
    int? GpsAlertEventId,
    DateTime CreatedAtUtc,
    DateTime? VerifiedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolvedBy);

public record DriverSubmitFuelPumpOrderRequest(
    int VehicleId,
    decimal OrderedLiters,
    string? StationName,
    double? Latitude,
    double? Longitude);

public record DriverSubmitFuelPumpOrderResultDto(
    int OrderId,
    string Status,
    decimal? FuelLevelAtOrderPercent,
    string Message);

public record FuelPumpOrderRow(
    int Id,
    int TenantId,
    int VehicleId,
    int DriverId,
    decimal OrderedLiters,
    decimal? FuelLevelAtOrderPercent,
    decimal? TankCapacityLiters,
    decimal? FuelLevelAfterPercent,
    decimal? ActualIncreaseLiters,
    decimal? ShortfallLiters,
    string? StationName,
    double? Latitude,
    double? Longitude,
    string Status,
    int? GpsAlertEventId,
    DateTime CreatedAtUtc);
