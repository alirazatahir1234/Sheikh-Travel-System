using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Users.DTOs;

public static class UserLifecycle
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Suspended = "Suspended";
    public const string Locked = "Locked";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Pending, Active, Inactive, Suspended, Locked
    };

    public static bool IsActiveStatus(string? status)
        => string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

    public static string FromIsActive(bool isActive)
        => isActive ? Active : Inactive;

    public static string Normalize(string? status, bool? isActive = null)
    {
        if (!string.IsNullOrWhiteSpace(status) && All.Contains(status))
            return All.First(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        if (isActive.HasValue)
            return FromIsActive(isActive.Value);
        return Active;
    }
}

public static class EmployeeTypes
{
    public const string Driver = "Driver";
    public const string Staff = "Staff";
    public const string Admin = "Admin";
    public const string Manager = "Manager";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Driver, Staff, Admin, Manager
    };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return All.FirstOrDefault(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase));
    }
}

public record UserDto(
    int Id,
    string FullName,
    string Email,
    string Phone,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    int? CompanyId = null,
    string? CompanyName = null,
    int? BranchId = null,
    string? BranchName = null,
    int? DepartmentId = null,
    string? DepartmentName = null,
    string? JobTitle = null,
    string? EmployeeCode = null,
    string? EmployeeType = null,
    string Status = UserLifecycle.Active,
    string? DefaultWorkspaceKey = null,
    string? DefaultDashboardKey = null,
    string? HomeRoute = null,
    string? TimeZone = null,
    string? Language = null,
    string? Theme = null,
    string? AvatarUrl = null);

public record CreateUserDto(
    string FullName,
    string Email,
    string Password,
    string Phone,
    UserRole Role,
    int? BranchId = null,
    int? DepartmentId = null,
    string? JobTitle = null,
    string? EmployeeCode = null,
    string? EmployeeType = null,
    string? Status = null,
    string? DefaultWorkspaceKey = null,
    string? DefaultDashboardKey = null,
    string? HomeRoute = null,
    string? TimeZone = null,
    string? Language = null,
    string? Theme = null,
    string? AvatarUrl = null);

public record UpdateUserDto(
    string FullName,
    string Email,
    string Phone,
    UserRole Role,
    bool IsActive,
    int? BranchId = null,
    int? DepartmentId = null,
    string? JobTitle = null,
    string? EmployeeCode = null,
    string? EmployeeType = null,
    string? Status = null,
    string? DefaultWorkspaceKey = null,
    string? DefaultDashboardKey = null,
    string? HomeRoute = null,
    string? TimeZone = null,
    string? Language = null,
    string? Theme = null,
    string? AvatarUrl = null);

public record UserProfileDto(
    int Id,
    string FullName,
    string Email,
    string? Phone,
    string? JobTitle,
    string? EmployeeCode,
    string? EmployeeType,
    string Status,
    int? CompanyId,
    string? CompanyName,
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    string? DefaultWorkspaceKey,
    string? DefaultDashboardKey,
    string? HomeRoute,
    string? TimeZone,
    string? Language,
    string? Theme,
    string? AvatarUrl);

public record CompanyUserSummaryDto(
    int CompanyId,
    int TotalUsers,
    int Drivers,
    int Managers,
    int Administrators,
    int Staff,
    int DepartmentCount);
