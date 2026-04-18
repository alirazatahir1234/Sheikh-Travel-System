using SheikhTravelSystem.Domain.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

public class FuelLog : BaseEntity
{
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public decimal Liters { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal TotalCost { get; set; }
    public decimal OdometerReading { get; set; }
    public FuelType FuelType { get; set; }
    public DateTime FuelDate { get; set; }
    public string? Station { get; set; }
}
