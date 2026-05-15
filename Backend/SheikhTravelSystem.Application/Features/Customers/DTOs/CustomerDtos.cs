namespace SheikhTravelSystem.Application.Features.Customers.DTOs;

public record CustomerDto(
    int Id,
    string FullName,
    string Phone,
    string? Email,
    string? Address,
    string? CNIC,
    bool IsActive,
    DateTime CreatedAt,
    string? FatherOrHusbandName = null,
    string? Gender = null,
    DateTime? DateOfBirth = null,
    string? Nationality = null);

public record CreateCustomerDto(
    string FullName,
    string Phone,
    string? Email,
    string? Address,
    string? CNIC,
    string? FatherOrHusbandName = null,
    string? Gender = null,
    DateTime? DateOfBirth = null,
    string? Nationality = null);

public record UpdateCustomerDto(
    string FullName,
    string Phone,
    string? Email,
    string? Address,
    string? CNIC,
    string? FatherOrHusbandName = null,
    string? Gender = null,
    DateTime? DateOfBirth = null,
    string? Nationality = null);
