using SheikhTravelSystem.Domain.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

public class Vehicle : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string? VehicleCode { get; set; }
    public string? VIN { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Color { get; set; }
    public string? VehicleType { get; set; }
    public int SeatingCapacity { get; set; }
    public decimal FuelAverage { get; set; }
    public FuelType FuelType { get; set; }
    public string? EngineNo { get; set; }
    public string? ChassisNo { get; set; }
    public decimal CurrentMileage { get; set; }
    public decimal CurrentEngineHours { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public int? GpsDeviceId { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? PurchaseCurrencyCode { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    // Predictive maintenance (nullable → helpers apply fleet defaults)
    public decimal? OilIntervalKm { get; set; }
    public int? OilIntervalDays { get; set; }
    public DateTime? LastOilChangeDate { get; set; }
    public decimal? LastOilChangeMileage { get; set; }
    public DateTime? TireInstallDate { get; set; }
    public decimal? TireInstallMileage { get; set; }
    public decimal? TireExpectedLifeKm { get; set; }
    public decimal? EngineExpectedLifeKm { get; set; }
    public int? EngineExpectedLifeYears { get; set; }

    public bool IsActive => Status != VehicleStatus.Retired;
}
