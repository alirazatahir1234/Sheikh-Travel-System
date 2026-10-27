using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

public record FuelLogDto(
    int Id, int VehicleId, int? DriverId, decimal Liters, decimal PricePerLiter,
    decimal TotalCost, decimal OdometerReading, FuelType FuelType,
    DateTime FuelDate, string? Station, DateTime CreatedAt, string? ReceiptUrl = null,
    string? VehicleName = null, string? VehicleRegistration = null, string? DriverName = null);

public record CreateFuelLogDto(
    int VehicleId, int? DriverId, decimal Liters, decimal PricePerLiter,
    decimal OdometerReading, FuelType FuelType, DateTime FuelDate, string? Station,
    string? ReceiptUrl = null);

/// <summary>Latest OBD/GPS fuel telemetry for a vehicle (null fields = not reported by device).</summary>
public record VehicleFuelTelemetryDto(
    int VehicleId,
    string? VehicleName,
    DateTime? LastUpdate,
    decimal? FuelLevelPercent,
    decimal? FuelConsumptionRateLph,
    decimal? ObdFuelUsedLiters,
    decimal? ObdOdometerKm,
    string? IntegrationNote);

public record FuelEfficiencyRowDto(
    string GroupKey,
    string Label,
    decimal? DistanceKm,
    decimal Liters,
    decimal? KmPerLiter,
    string BasisNote);

public record FuelEfficiencyReportDto(
    DateTime From,
    DateTime To,
    IReadOnlyList<FuelEfficiencyRowDto> ByVehicle,
    IReadOnlyList<FuelEfficiencyRowDto> ByDriver,
    IReadOnlyList<FuelEfficiencyRowDto> ByRoute);
