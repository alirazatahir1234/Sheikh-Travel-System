using SheikhTravelSystem.Domain.Common;

namespace SheikhTravelSystem.Domain.Entities;

public class VehicleTracking : BaseEntity
{
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public int? BookingId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal Speed { get; set; }
    public DateTime Timestamp { get; set; }
}
