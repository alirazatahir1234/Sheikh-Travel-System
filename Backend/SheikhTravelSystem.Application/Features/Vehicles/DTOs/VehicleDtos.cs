using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Vehicles.DTOs;

public record VehicleDto(
    int Id, string Name, string RegistrationNumber, string? Model, int? Year,
    int SeatingCapacity, decimal FuelAverage, FuelType FuelType,
    decimal CurrentMileage, DateTime? InsuranceExpiryDate, VehicleStatus Status, DateTime CreatedAt);

public record CreateVehicleDto(
    string Name, string RegistrationNumber, string? Model, int? Year,
    int SeatingCapacity, decimal FuelAverage, FuelType FuelType,
    decimal CurrentMileage, DateTime? InsuranceExpiryDate);

public record UpdateVehicleDto(
    string Name, string RegistrationNumber, string? Model, int? Year,
    int SeatingCapacity, decimal FuelAverage, FuelType FuelType,
    decimal CurrentMileage, DateTime? InsuranceExpiryDate, VehicleStatus Status);
