namespace SheikhTravelSystem.Application.Features.Fleet.EvAnomaly;

public record EvFleetVehicleRowDto(
    int VehicleId,
    string VehicleName,
    string RegistrationNumber,
    decimal? BatteryPercent,
    decimal? ElectricRangeKm,
    decimal? EstimatedRangeKm,
    double? Latitude,
    double? Longitude,
    bool RangeAlert);

public record EvFleetDashboardDto(
    int ElectricVehicleCount,
    int RangeAlertCount,
    decimal? AverageBatteryPercent,
    IReadOnlyList<EvFleetVehicleRowDto> Vehicles,
    IReadOnlyList<ChargingStationDto> ChargingStations);

public record ChargingStationDto(
    int Id,
    string Name,
    double Latitude,
    double Longitude,
    string? Address,
    string? ConnectorType,
    bool IsActive);

public record UpsertChargingStationRequest(
    int? Id,
    string Name,
    double Latitude,
    double Longitude,
    string? Address,
    string? ConnectorType,
    bool IsActive);

public record FleetAnomalyEventDto(
    long Id,
    int VehicleId,
    string? VehicleName,
    int? DriverId,
    string? DriverName,
    string AnomalyType,
    string Severity,
    double? Latitude,
    double? Longitude,
    decimal? SpeedKmh,
    string? Details,
    bool IsReviewed,
    DateTime CreatedAtUtc,
    string? ReviewNotes);

public record ReviewFleetAnomalyRequest(string? Notes);

public record VehicleDocumentTrackerItemDto(
    int? ComplianceDocumentId,
    int VehicleId,
    string VehicleName,
    string DocumentType,
    DateTime? ExpiryDate,
    string Status,
    string Source);

public record UpsertComplianceDocumentRequest(
    int? Id,
    int VehicleId,
    string DocumentType,
    string? DocumentNumber,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string? FileUrl,
    string? Notes);

public record DriverEvStatusDto(
    int VehicleId,
    string VehicleName,
    decimal? BatteryPercent,
    decimal? ElectricRangeKm,
    decimal? EstimatedRangeKm,
    bool RangeAlert,
    IReadOnlyList<ChargingStationDto> NearbyStations);

public record VehicleDocumentExpiryDto(
    string DocumentType,
    DateTime? ExpiryDate,
    int DaysRemaining,
    string Status);
