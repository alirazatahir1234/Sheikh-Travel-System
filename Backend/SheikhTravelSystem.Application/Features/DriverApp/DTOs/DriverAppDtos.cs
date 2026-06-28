namespace SheikhTravelSystem.Application.Features.DriverApp.DTOs;

public record DriverLoginRequest(string Phone, string Password);

public record DriverAuthResultDto(
    string AccessToken,
    string RefreshToken,
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

public record DriverLocationBatchDto(List<DriverLocationDto> Positions);

public record DriverDashboardDto(
    int AssignedTripsToday,
    int CompletedToday,
    bool ClockedIn,
    string? CurrentVehicle,
    string? CurrentVehiclePlate,
    decimal EarningsThisWeek,
    int UnreadNotifications,
    string DriverStatus);

public record DriverProfileDto(
    int Id,
    string FullName,
    string Phone,
    string? Email,
    string? PhotoUrl,
    string DriverCode,
    string LicenseNumber,
    DateTime? LicenseExpiryDate,
    int Status,
    string StatusName,
    bool IsActive,
    string? CurrentVehicleName,
    string? CurrentVehiclePlate,
    string? BranchName,
    decimal? Rating,
    int? YearsExperience,
    string? VerificationStatus);

public record DriverAttendanceRecordDto(
    int Id,
    string AttendanceType,
    DateTime RecordedAt,
    double? Latitude,
    double? Longitude,
    string? Notes);

public record DriverCheckInRequest(double? Latitude, double? Longitude);
public record DriverCheckOutRequest(double? Latitude, double? Longitude);
