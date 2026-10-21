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
