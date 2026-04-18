using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers.DTOs;

public record DriverDto(
    int Id, string FullName, string Phone, string LicenseNumber,
    DateTime LicenseExpiryDate, string? CNIC, string? Address,
    DriverStatus Status, bool IsActive, DateTime CreatedAt);

public record CreateDriverDto(
    string FullName, string Phone, string LicenseNumber,
    DateTime LicenseExpiryDate, string? CNIC, string? Address);

public record UpdateDriverDto(
    string FullName, string Phone, string LicenseNumber,
    DateTime LicenseExpiryDate, string? CNIC, string? Address,
    DriverStatus Status, bool IsActive);
