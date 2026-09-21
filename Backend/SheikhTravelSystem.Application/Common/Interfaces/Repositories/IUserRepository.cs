using SheikhTravelSystem.Application.Features.Users.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for users and role assignments. SQL lives in Infrastructure.
/// </summary>
public interface IUserRepository
{
    Task<(IReadOnlyList<UserDto> Items, int TotalCount)> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        int? branchId,
        int? departmentId,
        string? status,
        string? employeeType,
        string? search,
        CancellationToken cancellationToken = default);

    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<CompanyUserSummaryDto> GetCompanyUserSummaryAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<int?> GetTenantIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserOrgInfo?> GetOrgInfoAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsInTenantAsync(
        string email,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsForOtherAsync(
        string email,
        int excludeUserId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<UserPasswordInfo?> GetPasswordInfoAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task EnsureOrgBelongsToTenantAsync(
        int tenantId,
        int? branchId,
        int? departmentId,
        CancellationToken cancellationToken = default);

    Task<int> InsertAsync(UserInsertModel model, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, UserUpdateModel model, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        int id,
        bool isActive,
        string status,
        CancellationToken cancellationToken = default);

    Task UpdateProfileAsync(
        int userId,
        string fullName,
        string? phone,
        string? timeZone,
        string? language,
        string? theme,
        string? avatarUrl,
        string? jobTitle,
        CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default);

    Task AdminResetPasswordAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignedRoleDto>> GetAssignedRolesAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetActiveRoleCodesAsync(
        int tenantId,
        IReadOnlyList<int> roleIds,
        CancellationToken cancellationToken = default);

    Task<bool> ActivePlatformRoleExistsAsync(
        int tenantId,
        string code,
        CancellationToken cancellationToken = default);

    Task SyncLegacyRoleAsync(
        int userId,
        int tenantId,
        UserRole legacyRole,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken = default);

    Task AssignPlatformRoleAsync(
        int userId,
        int tenantId,
        string platformRoleCode,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken = default);

    Task ReplaceRoleAssignmentsAsync(
        int userId,
        int tenantId,
        IReadOnlyList<int> roleIds,
        IReadOnlyDictionary<int, (int? BranchId, int? DepartmentId)> scopes,
        int? assignedBy,
        CancellationToken cancellationToken = default);
}

public sealed record UserOrgInfo(int TenantId, int? BranchId, int? DepartmentId);

public sealed record UserPasswordInfo(int Id, int TenantId, string PasswordHash);

public sealed record UserInsertModel(
    int TenantId,
    string FullName,
    string Email,
    string PasswordHash,
    string Phone,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime PasswordChangedAt,
    int? BranchId,
    int? DepartmentId,
    string? JobTitle,
    string? EmployeeCode,
    string? EmployeeType,
    string Status,
    string? DefaultWorkspaceKey,
    string? DefaultDashboardKey,
    string? HomeRoute,
    string? TimeZone,
    string? Language,
    string? Theme,
    string? AvatarUrl);

public sealed record UserUpdateModel(
    int TenantId,
    string FullName,
    string Email,
    string Phone,
    UserRole Role,
    bool IsActive,
    DateTime UpdatedAt,
    int? BranchId,
    int? DepartmentId,
    string? JobTitle,
    string? EmployeeCode,
    string? EmployeeType,
    string Status,
    string? DefaultWorkspaceKey,
    string? DefaultDashboardKey,
    string? HomeRoute,
    string? TimeZone,
    string? Language,
    string? Theme,
    string? AvatarUrl);
