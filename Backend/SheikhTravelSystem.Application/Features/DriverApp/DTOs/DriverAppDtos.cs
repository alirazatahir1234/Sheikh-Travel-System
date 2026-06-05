namespace SheikhTravelSystem.Application.Features.DriverApp.DTOs;

public record DriverLoginRequest(string Phone, string Password);

public record DriverAuthResultDto(
    string AccessToken,
    int DriverId,
    string FullName,
    string Phone);

public record DriverTripDto(
    int Id,
    string BookingNumber,
    string CustomerName,
    string RouteName,
    DateTime PickupTime,
    DateTime? DropoffTime,
    int Status,
    string StatusName,
    int? VehicleId,
    string? VehicleName,
    decimal TotalAmount);

public record DriverEarningsDto(
    decimal TripAllowances,
    decimal CompletedTripCount,
    DateTime FromDate,
    DateTime ToDate);

public record DriverLocationDto(
    int VehicleId,
    double Latitude,
    double Longitude,
    decimal Speed,
    int? BookingId);
