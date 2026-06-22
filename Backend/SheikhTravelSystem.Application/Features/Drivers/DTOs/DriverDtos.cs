using System.Text.Json.Serialization;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers.DTOs;

public record DriverListItemDto(
    int Id,
    string? DriverCode,
    string? FirstName,
    string? LastName,
    string FullName,
    string Phone,
    string LicenseNumber,
    DateTime LicenseExpiryDate,
    bool LicenseExpired,
    bool LicenseExpiringSoon,
    string? Nationality,
    DriverStatus Status,
    bool IsActive,
    string VerificationStatus,
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    DateTime? HireDate,
    int? AssignedVehicleId,
    string? AssignedVehicleCode,
    string? AssignedVehicleRegistration,
    string? AssignedVehicleName,
    string? AssignedVehicleMake,
    string? AssignedVehicleModel,
    string? AssignedVehicleColor,
    decimal? Rating,
    bool GpsOnline,
    string? AvailabilityBucket,
    DateTime CreatedAt);

public record DriverDto(
    int Id,
    string? DriverCode,
    string? FirstName,
    string? LastName,
    string FullName,
    string Phone,
    string LicenseNumber,
    DateTime LicenseExpiryDate,
    bool LicenseExpired,
    bool LicenseExpiringSoon,
    [property: JsonPropertyName("cnic")] string? CNIC,
    string? Address,
    string? Nationality,
    string? Email,
    DateTime? DateOfBirth,
    string? Gender,
    string? EmergencyContactName,
    string? EmergencyContact,
    DateTime? HireDate,
    string? PhotoUrl,
    string VerificationStatus,
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    int? AssignedVehicleId,
    string? AssignedVehicleCode,
    string? AssignedVehicleRegistration,
    string? AssignedVehicleName,
    string? AssignedVehicleMake,
    string? AssignedVehicleModel,
    string? AssignedVehicleColor,
    DriverStatus Status,
    bool IsActive,
    decimal? Rating,
    int? YearsExperience,
    bool GpsOnline,
    string? AvailabilityBucket,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateDriverDto(
    string FirstName,
    string LastName,
    string Phone,
    string LicenseNumber,
    DateTime LicenseExpiryDate,
    string? Nationality = null,
    string? Email = null,
    DateTime? DateOfBirth = null,
    string? Gender = null,
    string? EmergencyContactName = null,
    string? EmergencyContact = null,
    DateTime? HireDate = null,
    int? BranchId = null,
    int? DepartmentId = null,
    [property: JsonPropertyName("cnic")] string? CNIC = null,
    string? Address = null);

public record UpdateDriverDto(
    string FirstName,
    string LastName,
    string Phone,
    string LicenseNumber,
    DateTime LicenseExpiryDate,
    DriverStatus Status,
    bool IsActive,
    string? Nationality = null,
    string? Email = null,
    DateTime? DateOfBirth = null,
    string? Gender = null,
    string? EmergencyContactName = null,
    string? EmergencyContact = null,
    DateTime? HireDate = null,
    int? BranchId = null,
    int? DepartmentId = null,
    [property: JsonPropertyName("cnic")] string? CNIC = null,
    string? Address = null);

public record DriverStatsDto(
    int TotalDrivers,
    int Active,
    int Inactive,
    int OnTrip,
    int OffDuty,
    int Available,
    int Busy,
    int OnLeave,
    int Suspended,
    int GpsOnline,
    int LicensesExpiringSoon,
    int LicensesExpiringIn7Days,
    int LicensesExpired,
    int VerifiedDrivers,
    int PendingVerification,
    int AssignedDrivers);

public record DriverTimelineEventDto(
    int Id,
    string EventType,
    string Title,
    string? Description,
    DateTime OccurredAt);

public record AssignDriverVehicleRequest(
    int VehicleId,
    int? BookingId = null,
    string? AssignmentType = null,
    string? Remarks = null,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null);

public record UpdateDriverVerificationRequest(string VerificationStatus);

public record UploadDriverDocumentResult(int DocumentId, string FileUrl, string DocumentType);

public record DriverAvailabilityDto(
    bool PhoneAvailable,
    bool EmailAvailable,
    bool LicenseAvailable);

public record TransferDriverVehicleRequest(
    int NewVehicleId,
    int? BookingId = null,
    string? AssignmentType = null,
    string? Remarks = null,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null);

public record DriverAssignmentDto(
    int Id,
    int VehicleId,
    string? VehicleRegistration,
    string? VehicleCode,
    string? VehicleName,
    string? VehicleMake,
    string? VehicleModel,
    string? VehicleColor,
    string AssignmentType,
    string Status,
    DateTime StartAt,
    DateTime? EndAt,
    int? BookingId,
    string? AssignedBy,
    string? Remarks);

public record DriversAvailabilitySummaryDto(
    int Available,
    int Busy,
    int OnTrip,
    int Unavailable);

public record DriverAvailabilityDetailDto(
    int DriverId,
    string AvailabilityBucket,
    DriverStatus Status,
    bool HasActiveAssignment);

public record DriverPerformanceSummaryDto(
    int DriverId,
    string DriverName,
    decimal? Rating,
    int? YearsExperience,
    int TotalTrips,
    int CompletedTrips,
    decimal TotalRevenue,
    decimal CompletionRate,
    int ViolationCount,
    int AttendancePresentCount);

public record CreateDriverViolationRequest(
    string ViolationType,
    string Severity,
    DateTime OccurredAt,
    string? Description = null,
    int? BookingId = null,
    int? GpsAlertId = null);

public record DriverViolationDto(
    int Id,
    string ViolationType,
    string Severity,
    DateTime OccurredAt,
    string? Description,
    int? BookingId,
    string Status,
    DateTime CreatedAt);

public record CreateDriverAttendanceRequest(
    DateTime AttendanceDate,
    string Status,
    DateTime? CheckInAt = null,
    DateTime? CheckOutAt = null,
    string? Notes = null);

public record DriverAttendanceDto(
    int Id,
    DateTime AttendanceDate,
    string Status,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    string? Notes,
    DateTime CreatedAt);

public record DriverLocationDto(
    decimal? Latitude,
    decimal? Longitude,
    decimal? Speed,
    bool? Ignition,
    DateTime? LastSeen,
    int? VehicleId,
    string? VehicleRegistration,
    bool GpsOnline);

public record DriverLocationPointDto(
    decimal Latitude,
    decimal Longitude,
    decimal? Speed,
    DateTime Timestamp);

public record UpdateDriverRatingRequest(decimal Rating);

// ── Driver Verification ──────────────────────────────────────────────────────

public record DriverDocumentDetailedDto(
    int Id,
    string DocumentType,
    string? FileUrl,
    DateTime? ExpiryDate,
    string Status,
    string? RejectionReason,
    string? ReviewedBy,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

public record UpdateDocumentStatusRequest(
    string Status,
    string? RejectionReason = null);

public record AddDriverReviewNoteRequest(
    string Note,
    string? DocumentType = null);

public record DriverReviewNoteDto(
    int Id,
    string Note,
    string? DocumentType,
    string? CreatedBy,
    DateTime CreatedAt);
