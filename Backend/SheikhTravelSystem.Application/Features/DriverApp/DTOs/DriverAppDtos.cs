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
    decimal TotalAmount,
    string? PickupAddress = null,
    double? PickupLatitude = null,
    double? PickupLongitude = null,
    string? DropoffAddress = null,
    double? DropLatitude = null,
    double? DropLongitude = null,
    string? GoogleMapsUrl = null,
    string? GoogleDirectionsUrl = null,
    /// <summary>Operational Trips.Id when source is Trip; otherwise null.</summary>
    int? TripId = null,
    int? BookingId = null,
    /// <summary>"Trip" or "Booking".</summary>
    string Source = "Booking",
    /// <summary>ERP TripStatus int (mapped for booking rows).</summary>
    int LifecycleStatus = 0,
    string LifecycleStatusName = "",
    IReadOnlyList<string>? NextActions = null);

public record DriverEarningsDto(
    decimal TripAllowances,
    decimal CompletedTripCount,
    DateTime FromDate,
    DateTime ToDate,
    decimal Today = 0,
    decimal ThisWeek = 0,
    decimal ThisMonth = 0,
    decimal Pending = 0,
    decimal Paid = 0,
    decimal FuelCost = 0,
    decimal DistanceKm = 0,
    decimal HoursWorked = 0,
    IReadOnlyList<DriverEarningsDayDto>? Daily = null);

public record DriverEarningsDayDto(DateTime Date, decimal Amount, int TripCount);

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

public record DriverSosRequest(double? Latitude, double? Longitude, string? Message);

public record DriverSosResultDto(int Id, DateTime CreatedAt);

public record DriverTimelineEventDto(
    int Id,
    string EventType,
    string Title,
    DateTime EventTime,
    string? Description,
    string? Status,
    int? ReferenceId);

public record DriverFuelReceiptDto(
    int Id,
    int VehicleId,
    string? VehicleName,
    string? VehiclePlate,
    decimal Liters,
    decimal PricePerLiter,
    decimal TotalCost,
    decimal? OdometerReading,
    string FuelTypeName,
    DateTime FuelDate,
    string? Station,
    string? ReceiptUrl);

public record FuelReceiptOcrSuggestionDto(
    decimal? Liters,
    decimal? PricePerLiter,
    decimal? TotalCost,
    string? Station,
    string? FuelType,
    int Confidence,
    string? RawText);

public record RegisterDriverDeviceRequest(
    string DeviceId,
    string Platform,
    string? Model = null,
    string? OsVersion = null,
    string? AppVersion = null,
    string? PackageName = null,
    string? InstallerStore = null,
    string? FingerprintHash = null,
    bool IsEmulator = false,
    bool IsRooted = false,
    bool IsJailbroken = false,
    bool IsTampered = false,
    bool PinningConfigured = false);

public record DriverDeviceDto(
    int Id,
    string DeviceId,
    string Platform,
    string? Model,
    bool IsEmulator,
    bool IsCompromised,
    bool IsTampered,
    DateTime LastSeenAt);
