using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.DriverApp.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for driver mobile app. SQL lives in Infrastructure.
/// </summary>
public interface IDriverAppRepository
{
    // Auth / profile / dashboard
    Task<DriverLoginRow?> GetDriverLoginByPhoneAsync(string phone, int tenantId, CancellationToken ct = default);
    Task UpdateUserRefreshTokenAsync(int userId, string token, DateTime expiry, CancellationToken ct = default);
    Task<DriverProfileDto?> GetProfileAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<DriverDashboardStatsRow?> GetDashboardStatsAsync(int driverId, int tenantId, int? userId, DateTime today, DateTime weekStart, CancellationToken ct = default);
    Task<int?> GetDriverStatusAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<int> SetDriverStatusAsync(int driverId, int tenantId, int status, CancellationToken ct = default);

    // Ownership / bookings
    Task<bool> OwnsBookingAsync(int bookingId, int driverId, CancellationToken ct = default);
    Task<bool> DriverOwnsVehicleAsync(int driverId, int vehicleId, int tenantId, CancellationToken ct = default);
    Task<string?> GetVehicleNameAsync(int vehicleId, CancellationToken ct = default);
    Task<DriverBookingRef?> ResolveDriverBookingAsync(int id, int driverId, int tenantId, CancellationToken ct = default);
    Task<decimal> GetBookingPaidAmountAsync(int bookingId, CancellationToken ct = default);

    // Location
    Task<(int VehicleId, int? BookingId)?> GetActiveTripVehicleAsync(int driverId, CancellationToken ct = default);
    Task<(int VehicleId, int? BookingId)?> GetActiveBookingVehicleAsync(int driverId, CancellationToken ct = default);

    // Attendance
    Task<int> UpdateCheckInAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default);
    Task InsertCheckInAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default);
    Task<int> UpdateCheckOutAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default);
    Task InsertCheckOutAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default);
    Task<IReadOnlyList<DriverAttendanceRecordDto>> GetAttendanceHistoryAsync(int driverId, DateTime from, DateTime to, int offset, int size, CancellationToken ct = default);

    // SOS
    Task<(string FullName, string Phone)?> GetDriverNamePhoneAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<(int? VehicleId, int? BookingId)> GetStartedBookingForSosAsync(int driverId, CancellationToken ct = default);
    Task<int> InsertSosAlertAsync(int tenantId, int driverId, int? vehicleId, int? bookingId, double? lat, double? lng, string? message, DateTime createdAt, CancellationToken ct = default);

    // Trips list
    Task<IReadOnlyList<DriverOpTripRow>> GetOperationalTripsAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DriverLegacyTripRow>> GetLegacyBookingTripsAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<(int BookingId, decimal PaidAmount)>> GetPaidAmountsForBookingsAsync(IReadOnlyList<int> bookingIds, CancellationToken ct = default);

    // Timeline / earnings
    Task<IReadOnlyList<DriverTimelineEventDto>> GetTimelineAsync(int driverId, int tenantId, int? userId, int offset, int size, CancellationToken ct = default);
    Task<decimal> SumPaymentsAsync(int driverId, DateTime from, DateTime to, int? statusFilter, CancellationToken ct = default);
    Task<decimal> SumPendingPartialPaymentsAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> CountCompletedBookingsAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<decimal> SumFuelCostAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<decimal> SumBookingDistanceAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<decimal?> SumTripDistanceAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<decimal> SumBookingHoursAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<(DateTime Day, decimal Amount, int TripCount)>> GetDailyEarningsAsync(int driverId, DateTime from, DateTime toExclusive, CancellationToken ct = default);

    // Fuel
    Task<IReadOnlyList<DriverFuelReceiptRow>> GetFuelReceiptsAsync(int driverId, int offset, int size, CancellationToken ct = default);

    // Inspection
    Task<(int Id, string Name, string? Description, string ChecklistJson)?> GetInspectionTemplateAsync(int tenantId, CancellationToken ct = default);
    Task<(int Id, string ChecklistJson)?> GetDefaultInspectionTemplateAsync(int tenantId, CancellationToken ct = default);
    Task<string?> GetInspectionChecklistJsonAsync(int templateId, CancellationToken ct = default);
    Task<bool> VehicleExistsForTenantAsync(int vehicleId, int tenantId, CancellationToken ct = default);
    Task<string?> GetDriverFullNameAsync(int driverId, CancellationToken ct = default);
    Task<int> InsertInspectionAsync(DriverInspectionInsert insert, CancellationToken ct = default);
    Task UpdateInspectionMediaAsync(int id, string photosJson, string? signatureUrl, CancellationToken ct = default);
    Task<IReadOnlyList<DriverInspectionHistoryRow>> GetInspectionHistoryAsync(int driverId, int tenantId, int offset, int size, CancellationToken ct = default);
    Task<IReadOnlyList<DriverInspectionVehicleDto>> GetVehiclesForInspectionAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DriverInspectionVehicleDto>> GetFallbackVehiclesForInspectionAsync(int tenantId, CancellationToken ct = default);

    // Device
    Task<int?> UpsertDriverDeviceAsync(DriverDeviceUpsert upsert, CancellationToken ct = default);

    // Documents
    Task<(string? Cnic, DateTime? LicenseExpiry)?> GetDriverComplianceProfileAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DriverComplianceDocRow>> GetDriverComplianceDocsAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<(int Id, string Name)?> GetPrimaryAssignedVehicleAsync(int driverId, int tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleComplianceDocRow>> GetVehicleComplianceDocsAsync(int vehicleId, int tenantId, CancellationToken ct = default);

    // Trip lifecycle
    Task<DriverTripRef?> FindDriverTripAsync(int id, int driverId, int tenantId, CancellationToken ct = default);
    Task<bool> OwnsBookingForTenantAsync(int bookingId, int driverId, int tenantId, CancellationToken ct = default);
    Task<DriverTripRef?> FindTripByBookingAsync(int bookingId, int driverId, int tenantId, CancellationToken ct = default);
    Task<DriverTripRef?> GetTripRefAsync(int tripId, int driverId, int tenantId, CancellationToken ct = default);
    Task EnsureTripVehicleAsync(int tripId, int? bookingId, int driverId, int tenantId, CancellationToken ct = default);
    Task<int?> GetBookingStatusAsync(int bookingId, CancellationToken ct = default);
    Task SyncLinkedBookingStatusAsync(int bookingId, int status, int cancelledStatus, string? reason, CancellationToken ct = default);
}

public sealed class DriverLoginRow
{
    public int DriverId { get; set; }
    public int UserId { get; set; }
    public int TenantId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public sealed class DriverDashboardStatsRow
{
    public int Assigned { get; set; }
    public int Completed { get; set; }
    public bool ClockedIn { get; set; }
    public decimal WeekEarnings { get; set; }
    public int Unread { get; set; }
    public string? Vehicle { get; set; }
    public string? Plate { get; set; }
    public int Status { get; set; }
}

public sealed class DriverBookingRef
{
    public int BookingId { get; init; }
    public string BookingNumber { get; init; } = "";
    public decimal TotalAmount { get; init; }
}

public sealed class DriverOpTripRow
{
    public int Id { get; init; }
    public string? TripNumber { get; init; }
    public int? BookingId { get; init; }
    public string? BookingNumber { get; init; }
    public string? CustomerName { get; init; }
    public string? RouteName { get; init; }
    public DateTime PickupTime { get; init; }
    public DateTime? DropoffTime { get; init; }
    public int Status { get; init; }
    public int? VehicleId { get; init; }
    public string? VehicleName { get; init; }
    public decimal TotalAmount { get; init; }
    public string? PickupAddress { get; init; }
    public double? PickupLatitude { get; init; }
    public double? PickupLongitude { get; init; }
    public string? DropoffAddress { get; init; }
    public double? DropLatitude { get; init; }
    public double? DropLongitude { get; init; }
    public string? RouteSource { get; init; }
    public string? RouteDestination { get; init; }
}

public sealed class DriverLegacyTripRow
{
    public int Id { get; init; }
    public string BookingNumber { get; init; } = "";
    public string? CustomerName { get; init; }
    public string? RouteName { get; init; }
    public DateTime PickupTime { get; init; }
    public DateTime? DropoffTime { get; init; }
    public int Status { get; init; }
    public string? StatusName { get; init; }
    public int? VehicleId { get; init; }
    public string? VehicleName { get; init; }
    public decimal TotalAmount { get; init; }
    public string? PickupAddress { get; init; }
    public double? PickupLatitude { get; init; }
    public double? PickupLongitude { get; init; }
    public string? DropoffAddress { get; init; }
    public double? DropLatitude { get; init; }
    public double? DropLongitude { get; init; }
    public string? RouteSource { get; init; }
    public string? RouteDestination { get; init; }
}

public sealed class DriverFuelReceiptRow
{
    public int Id { get; init; }
    public int VehicleId { get; init; }
    public string? VehicleName { get; init; }
    public string? VehiclePlate { get; init; }
    public decimal Liters { get; init; }
    public decimal PricePerLiter { get; init; }
    public decimal TotalCost { get; init; }
    public decimal? OdometerReading { get; init; }
    public int FuelType { get; init; }
    public DateTime FuelDate { get; init; }
    public string? Station { get; init; }
    public string? ReceiptUrl { get; init; }
}

public sealed class DriverInspectionInsert
{
    public int TenantId { get; init; }
    public int VehicleId { get; init; }
    public int? TemplateId { get; init; }
    public int DriverId { get; init; }
    public string? InspectedBy { get; init; }
    public decimal? Odometer { get; init; }
    public string Result { get; init; } = "";
    public string ResultsJson { get; init; } = "";
    public string? Comments { get; init; }
    public string? CreatedBy { get; init; }
}

public sealed class DriverInspectionHistoryRow
{
    public int Id { get; init; }
    public int VehicleId { get; init; }
    public string? VehicleName { get; init; }
    public string? VehiclePlate { get; init; }
    public DateTime InspectionDate { get; init; }
    public string Result { get; init; } = "";
    public decimal? OdometerReading { get; init; }
    public string? Comments { get; init; }
    public string? PhotosJson { get; init; }
    public string? SignatureUrl { get; init; }
}

public sealed class DriverDeviceUpsert
{
    public int TenantId { get; init; }
    public int DriverId { get; init; }
    public int? UserId { get; init; }
    public string DeviceId { get; init; } = "";
    public string Platform { get; init; } = "";
    public string? Model { get; init; }
    public string? OsVersion { get; init; }
    public string? AppVersion { get; init; }
    public string? PackageName { get; init; }
    public string? InstallerStore { get; init; }
    public string? FingerprintHash { get; init; }
    public bool IsEmulator { get; init; }
    public bool IsRooted { get; init; }
    public bool IsJailbroken { get; init; }
    public bool IsTampered { get; init; }
    public bool PinningConfigured { get; init; }
    public DateTime Now { get; init; }
}

public sealed class DriverComplianceDocRow
{
    public int Id { get; init; }
    public string DocumentType { get; init; } = "";
    public string? FileUrl { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public string Status { get; init; } = "";
}

public sealed class VehicleComplianceDocRow
{
    public int Id { get; init; }
    public string DocumentType { get; init; } = "";
    public string? FileUrl { get; init; }
    public DateTime? ExpiryDate { get; init; }
}

public sealed class DriverTripRef
{
    public int Id { get; init; }
    public int Status { get; init; }
    public int? BookingId { get; init; }
    public int? VehicleId { get; init; }
}
