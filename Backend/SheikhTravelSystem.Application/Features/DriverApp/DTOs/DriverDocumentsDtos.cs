namespace SheikhTravelSystem.Application.Features.DriverApp.DTOs;

public record DriverAppDocumentDto(
    int? Id,
    string Scope,          // Driver | Vehicle
    string DocumentType,
    string Title,
    string? PreviewUrl,
    DateTime? ExpiryDate,
    string Status,         // Missing | Pending | Approved | Rejected | Valid | Expiring | Expired
    bool IsExpired,
    bool IsExpiringSoon,
    int? DaysUntilExpiry,
    bool CanUpload,
    int? VehicleId,
    string? VehicleName);

public record DriverAppDocumentsResponse(
    IReadOnlyList<DriverAppDocumentDto> Documents,
    DateTime? LicenseExpiryDate,
    string? CnicNumber,
    int ExpiringCount,
    int ExpiredCount,
    int MissingCount);
