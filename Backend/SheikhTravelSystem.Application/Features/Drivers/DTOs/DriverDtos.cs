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
    int? AssignedVehicleId,
    string? AssignedVehicleCode,
    string? AssignedVehicleRegistration,
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
    DriverStatus Status,
    bool IsActive,
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
    int OnTrip,
    int OffDuty,
    int Available,
    int OnLeave,
    int Suspended,
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

public record AssignDriverVehicleRequest(int VehicleId, int? BookingId = null, string? AssignmentType = null);

public record UpdateDriverVerificationRequest(string VerificationStatus);

public record UploadDriverDocumentResult(int DocumentId, string FileUrl, string DocumentType);

public record DriverAvailabilityDto(
    bool PhoneAvailable,
    bool EmailAvailable,
    bool LicenseAvailable);
