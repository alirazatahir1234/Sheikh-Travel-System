using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.DTOs;

public record BookingDto(
    int Id, string BookingNumber, int CustomerId, string? CustomerName, int RouteId, string? RouteName,
    int? VehicleId, string? VehicleName, int? DriverId, string? DriverName,
    DateTime PickupTime, DateTime? DropoffTime, int PassengerCount,
    decimal TotalAmount, BookingStatus Status, string? Notes, DateTime CreatedAt);

public record CreateBookingDto(
    int CustomerId, int RouteId, DateTime PickupTime, int PassengerCount,
    decimal TotalAmount, string? Notes);

public record UpdateBookingDto(
    int CustomerId, int RouteId, DateTime PickupTime, int PassengerCount,
    decimal TotalAmount, int? VehicleId, int? DriverId, string? Notes);

public record AssignDriverDto(int DriverId);

public record AssignVehicleDto(int VehicleId);
