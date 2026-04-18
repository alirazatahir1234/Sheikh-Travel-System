using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Users.DTOs;

public record UserDto(int Id, string FullName, string Email, string Phone, UserRole Role, bool IsActive, DateTime CreatedAt);

public record CreateUserDto(string FullName, string Email, string Password, string Phone, UserRole Role);

public record UpdateUserDto(string FullName, string Email, string Phone, UserRole Role, bool IsActive);
