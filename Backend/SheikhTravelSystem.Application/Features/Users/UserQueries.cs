using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Users;

/// <summary>Shared SQL/mapping for Stage 6 enriched user reads.</summary>
internal static class UserQueries
{
    public const string SelectSql = """
        SELECT u.Id, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.CreatedAt,
               u.TenantId AS CompanyId, t.Name AS CompanyName,
               u.BranchId, br.Name AS BranchName,
               u.DepartmentId, d.Name AS DepartmentName,
               u.JobTitle, u.EmployeeCode, u.EmployeeType,
               COALESCE(u.Status, CASE WHEN u.IsActive = 1 THEN N'Active' ELSE N'Inactive' END) AS Status,
               u.DefaultWorkspaceKey, u.DefaultDashboardKey, u.HomeRoute,
               u.TimeZone, u.Language, u.Theme, u.AvatarUrl
        FROM Users u
        LEFT JOIN Tenants t ON t.Id = u.TenantId
        LEFT JOIN Branches br ON br.Id = u.BranchId
        LEFT JOIN Departments d ON d.Id = u.DepartmentId
        """;

    public sealed class UserRow
    {
        public int Id { get; init; }
        public string FullName { get; init; } = "";
        public string Email { get; init; } = "";
        public string Phone { get; init; } = "";
        public UserRole Role { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public int? CompanyId { get; init; }
        public string? CompanyName { get; init; }
        public int? BranchId { get; init; }
        public string? BranchName { get; init; }
        public int? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public string? JobTitle { get; init; }
        public string? EmployeeCode { get; init; }
        public string? EmployeeType { get; init; }
        public string Status { get; init; } = UserLifecycle.Active;
        public string? DefaultWorkspaceKey { get; init; }
        public string? DefaultDashboardKey { get; init; }
        public string? HomeRoute { get; init; }
        public string? TimeZone { get; init; }
        public string? Language { get; init; }
        public string? Theme { get; init; }
        public string? AvatarUrl { get; init; }
    }

    public static UserDto ToDto(
        UserRow row,
        IReadOnlyList<AssignedRoleDto>? assignedRoles = null) => new(
        row.Id,
        row.FullName,
        row.Email,
        row.Phone ?? "",
        row.Role,
        row.IsActive,
        row.CreatedAt,
        row.CompanyId,
        row.CompanyName,
        row.BranchId,
        row.BranchName,
        row.DepartmentId,
        row.DepartmentName,
        row.JobTitle,
        row.EmployeeCode,
        row.EmployeeType,
        UserLifecycle.Normalize(row.Status, row.IsActive),
        row.DefaultWorkspaceKey,
        row.DefaultDashboardKey,
        row.HomeRoute,
        row.TimeZone,
        row.Language,
        row.Theme,
        row.AvatarUrl,
        assignedRoles);

    public static UserProfileDto ToProfileDto(
        UserRow row,
        IReadOnlyList<AssignedRoleDto>? assignedRoles = null,
        IReadOnlyList<EffectivePermissionDto>? effectivePermissions = null) => new(
        row.Id,
        row.FullName,
        row.Email,
        row.Phone,
        row.JobTitle,
        row.EmployeeCode,
        row.EmployeeType,
        UserLifecycle.Normalize(row.Status, row.IsActive),
        row.CompanyId,
        row.CompanyName,
        row.BranchId,
        row.BranchName,
        row.DepartmentId,
        row.DepartmentName,
        row.DefaultWorkspaceKey,
        row.DefaultDashboardKey,
        row.HomeRoute,
        row.TimeZone,
        row.Language,
        row.Theme,
        row.AvatarUrl,
        assignedRoles,
        effectivePermissions);

    public static async Task<Dictionary<int, List<AssignedRoleDto>>> LoadAssignedRolesMapAsync(
        IDbConnection connection,
        IReadOnlyList<int> userIds,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<int, List<AssignedRoleDto>>();
        if (userIds.Count == 0) return map;

        try
        {
            var rows = await connection.QueryAsync<(
                int UserId, int RoleId, string Code, string Name, string? DisplayName,
                string? Category, string? RoleType, int? BranchId, int? DepartmentId)>(
                new CommandDefinition("""
                    SELECT ur.UserId, r.Id AS RoleId, r.Code, r.Name,
                           COALESCE(r.DisplayName, r.Name) AS DisplayName,
                           r.Category,
                           COALESCE(r.RoleType, CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END) AS RoleType,
                           ur.BranchId, ur.DepartmentId
                    FROM UserRoles ur
                    INNER JOIN Roles r ON r.Id = ur.RoleId
                    WHERE ur.UserId IN @UserIds
                    ORDER BY COALESCE(r.SortOrder, 0), r.Name
                    """,
                    new { UserIds = userIds },
                    cancellationToken: cancellationToken));

            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.UserId, out var list))
                {
                    list = [];
                    map[row.UserId] = list;
                }

                list.Add(new AssignedRoleDto(
                    row.RoleId,
                    row.Code,
                    row.Name,
                    string.IsNullOrWhiteSpace(row.DisplayName) ? row.Name : row.DisplayName!,
                    row.Category,
                    row.RoleType,
                    row.BranchId,
                    row.DepartmentId));
            }
        }
        catch
        {
            // UserRoles / metadata may be unavailable.
        }

        return map;
    }

    public static async Task EnsureOrgBelongsToTenantAsync(
        IDbConnection connection,
        int tenantId,
        int? branchId,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        if (branchId.HasValue)
        {
            var ok = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Branches WHERE Id = @BranchId AND TenantId = @TenantId
                ) THEN 1 ELSE 0 END
                """, new { BranchId = branchId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));
            if (!ok)
                throw new Common.Exceptions.ConflictException("Branch does not belong to this company.");
        }

        if (departmentId.HasValue)
        {
            var ok = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Departments WHERE Id = @DepartmentId AND TenantId = @TenantId
                ) THEN 1 ELSE 0 END
                """, new { DepartmentId = departmentId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));
            if (!ok)
                throw new Common.Exceptions.ConflictException("Department does not belong to this company.");
        }
    }

    public static async Task<CompanyUserSummaryDto> LoadCompanyUserSummaryAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<(
            int TotalUsers, int Drivers, int Managers, int Administrators, int Staff, int DepartmentCount)>(
            new CommandDefinition("""
                SELECT
                    (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0) AS TotalUsers,
                    (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                        AND EmployeeType = N'Driver') AS Drivers,
                    (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                        AND EmployeeType = N'Manager') AS Managers,
                    (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                        AND EmployeeType = N'Admin') AS Administrators,
                    (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0
                        AND EmployeeType = N'Staff') AS Staff,
                    (SELECT COUNT(*) FROM Departments WHERE TenantId = @TenantId) AS DepartmentCount
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        return new CompanyUserSummaryDto(
            tenantId,
            row.TotalUsers,
            row.Drivers,
            row.Managers,
            row.Administrators,
            row.Staff,
            row.DepartmentCount);
    }
}
