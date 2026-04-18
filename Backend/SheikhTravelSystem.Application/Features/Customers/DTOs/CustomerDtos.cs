namespace SheikhTravelSystem.Application.Features.Customers.DTOs;

public record CustomerDto(int Id, string FullName, string Phone, string? Email, string? Address, string? CNIC, bool IsActive, DateTime CreatedAt);

public record CreateCustomerDto(string FullName, string Phone, string? Email, string? Address, string? CNIC);

public record UpdateCustomerDto(string FullName, string Phone, string? Email, string? Address, string? CNIC);
