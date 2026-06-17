using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Vehicles.DTOs;

public record VehicleListItemDto(
    int Id, string Name, string RegistrationNumber, string? VehicleCode, string? VIN,
    string? Make, string? Model, int? Year, string? VehicleType,
    int SeatingCapacity, decimal FuelAverage, FuelType FuelType,
    decimal CurrentMileage, DateTime? InsuranceExpiryDate,
    VehicleStatus Status, int? BranchId, DateTime CreatedAt,
    string? DriverName, int? DriverId, string? GpsImei, string? GpsSim, bool? EngineIgnition,
    DateTime? GpsLastSeenAt, bool GpsOnline, bool HasGpsDevice,
    double? LocationLatitude, double? LocationLongitude, DateTime? LocationLastUpdate,
    DateTime? NextServiceDue, decimal? NextDueMileage, string? ServiceAlert,
    string? ImageUrl);

public record VehicleDto(
    int Id, string Name, string RegistrationNumber, string? VehicleCode, string? VIN,
    string? Make, string? Model, int? Year, string? Color, string? VehicleType,
    int SeatingCapacity, decimal FuelAverage, FuelType FuelType,
    string? EngineNo, string? ChassisNo,
    decimal CurrentMileage, DateTime? InsuranceExpiryDate, int? GpsDeviceId,
    DateTime? PurchaseDate, decimal? PurchasePrice, int? BranchId, int? DepartmentId,
    VehicleStatus Status, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);

public record CreateVehicleDto(
    string Name, string RegistrationNumber, string? Model, int? Year,
    int SeatingCapacity, decimal FuelAverage, FuelType FuelType,
    decimal CurrentMileage, DateTime? InsuranceExpiryDate,
    string? VehicleCode = null, string? VIN = null, string? Make = null,
    string? Color = null, string? VehicleType = null,
    string? EngineNo = null, string? ChassisNo = null,
    DateTime? PurchaseDate = null, decimal? PurchasePrice = null,
    int? BranchId = null, int? DepartmentId = null);

public record UpdateVehicleDto(
    string Name, string RegistrationNumber, string? Model, int? Year,
    int SeatingCapacity, decimal FuelAverage, FuelType FuelType,
    decimal CurrentMileage, DateTime? InsuranceExpiryDate, VehicleStatus Status,
    string? VehicleCode = null, string? VIN = null, string? Make = null,
    string? Color = null, string? VehicleType = null,
    string? EngineNo = null, string? ChassisNo = null,
    DateTime? PurchaseDate = null, decimal? PurchasePrice = null,
    int? BranchId = null, int? DepartmentId = null);

public record VehicleMaintenanceDto(
    int Id, int VehicleId, string Description, decimal Cost,
    DateTime MaintenanceDate, DateTime? NextDueDate,
    MaintenanceStatus Status, string? ServiceProvider, DateTime CreatedAt);

public record VehicleFuelDto(
    int Id, int VehicleId, int? DriverId, decimal Liters, decimal PricePerLiter,
    decimal TotalCost, decimal OdometerReading, FuelType FuelType,
    DateTime FuelDate, string? Station, DateTime CreatedAt);

public record VehicleFuelSummaryDto(
    IReadOnlyList<VehicleFuelDto> Items, decimal TotalLiters, decimal TotalCost, int TotalCount);

public record VehicleGpsDto(
    int? GpsDeviceId, string? DeviceName, string? UniqueId, bool? IsActive,
    DateTime? LastSeenAt, bool? LastIgnition,
    double? Latitude, double? Longitude, decimal? Speed, DateTime? LastUpdate);

public record ChangeVehicleStatusRequest(VehicleStatus Status, string? Reason);

public record AssignVehicleDriverRequest(int DriverId, int? BookingId, string? AssignmentType);

public record AssignVehicleGpsRequest(int GpsDeviceId);
