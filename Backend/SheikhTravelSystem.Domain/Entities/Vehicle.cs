using SheikhTravelSystem.Domain.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

public class Vehicle : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int? Year { get; set; }
    public int SeatingCapacity { get; set; }
    public decimal FuelAverage { get; set; }
    public FuelType FuelType { get; set; }
    public decimal CurrentMileage { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
}
