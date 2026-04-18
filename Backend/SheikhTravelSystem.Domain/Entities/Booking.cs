using SheikhTravelSystem.Domain.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

public class Booking : BaseEntity
{
    public int CustomerId { get; set; }
    public int RouteId { get; set; }
    public int? VehicleId { get; set; }
    public int? DriverId { get; set; }
    public DateTime PickupTime { get; set; }
    public DateTime? DropoffTime { get; set; }
    public int PassengerCount { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? Notes { get; set; }

    // Navigation (not populated by Dapper unless explicit join)
    public Customer? Customer { get; set; }
    public Route? Route { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Driver? Driver { get; set; }
}
