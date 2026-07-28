using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Users.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Users;

/// <summary>Stage 7 helpers for UserRoles assignment + legacy sync.</summary>
internal static class UserRoleAssignment
{
    public sealed class AssignedRoleRow
    {
        public int RoleId { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string? DisplayName { get; init; }
        public string? Category { get; init; }
        public string? RoleType { get; init; }
        public int? BranchId { get; init; }
        public int? DepartmentId { get; init; }
    }

    public static async Task<IReadOnlyList<AssignedRoleRow>> LoadAssignedAsync(
        IDbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await connection.QueryAsync<AssignedRoleRow>(new CommandDefinition("""
                SELECT r.Id AS RoleId, r.Code, r.Name,
                       COALESCE(r.DisplayName, r.Name) AS DisplayName,
                       r.Category, COALESCE(r.RoleType, CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END) AS RoleType,
                       ur.BranchId, ur.DepartmentId
                FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY COALESCE(r.SortOrder, 0), r.Name
                """, new { UserId = userId }, cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            return (await connection.QueryAsync<AssignedRoleRow>(new CommandDefinition("""
                SELECT r.Id AS RoleId, r.Code, r.Name,
                       r.Name AS DisplayName,
                       CAST(NULL AS NVARCHAR(100)) AS Category,
                       CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END AS RoleType,
                       CAST(NULL AS INT) AS BranchId,
                       CAST(NULL AS INT) AS DepartmentId
                FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY r.Name
                """, new { UserId = userId }, cancellationToken: cancellationToken))).ToList();
        }
    }

    /// <summary>
    /// Ensures the legacy enum maps to a UserRoles row without removing other business roles.
    /// </summary>
    public static async Task SyncLegacyRoleAsync(
        IDbConnection connection,
        int userId,
        int tenantId,
        UserRole legacyRole,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken)
    {
        var code = RoleRegistrySeed.MapLegacyRoleCode(legacyRole);
        var roleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Roles WHERE TenantId = @TenantId AND Code = @Code
            """, new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));

        if (!roleId.HasValue)
            return;

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId) THEN 1 ELSE 0 END
            """, new { UserId = userId, RoleId = roleId.Value }, cancellationToken: cancellationToken));

        if (exists)
            return;

        await InsertAssignmentAsync(
            connection, userId, roleId.Value, branchId, departmentId, assignedBy, cancellationToken);
    }

    /// <summary>Assigns a platform role by code without removing other assignments.</summary>
    public static async Task AssignPlatformRoleAsync(
        IDbConnection connection,
        int userId,
        int tenantId,
        string platformRoleCode,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(platformRoleCode))
            return;

        var code = platformRoleCode.Trim().ToUpperInvariant();
        var roleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Roles WHERE TenantId = @TenantId AND Code = @Code AND IsActive = 1
            """, new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken));

        if (!roleId.HasValue)
            throw new Common.Exceptions.NotFoundException("Role", code);

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId) THEN 1 ELSE 0 END
            """, new { UserId = userId, RoleId = roleId.Value }, cancellationToken: cancellationToken));

        if (exists)
            return;

        await InsertAssignmentAsync(
            connection, userId, roleId.Value, branchId, departmentId, assignedBy, cancellationToken);
    }

    public static async Task ReplaceAssignmentsAsync(
        IDbConnection connection,
        int userId,
        int tenantId,
        IReadOnlyList<int> roleIds,
        IReadOnlyDictionary<int, (int? BranchId, int? DepartmentId)> scopes,
        int? assignedBy,
        CancellationToken cancellationToken)
    {
        var distinct = roleIds.Distinct().ToList();
        if (distinct.Count > 0)
        {
            var validCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM Roles WHERE TenantId = @TenantId AND Id IN @RoleIds AND IsActive = 1
                """, new { TenantId = tenantId, RoleIds = distinct }, cancellationToken: cancellationToken));
            if (validCount != distinct.Count)
                throw new Common.Exceptions.ConflictException(
                    "One or more roles are invalid, inactive, or do not belong to this company.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM UserRoles WHERE UserId = @UserId",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        foreach (var roleId in distinct)
        {
            scopes.TryGetValue(roleId, out var scope);
            await InsertAssignmentAsync(
                connection, userId, roleId, scope.BranchId, scope.DepartmentId, assignedBy, cancellationToken);
        }
    }

    private static async Task InsertAssignmentAsync(
        IDbConnection connection,
        int userId,
        int roleId,
        int? branchId,
        int? departmentId,
        int? assignedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO UserRoles (UserId, RoleId, BranchId, DepartmentId, AssignedAt, AssignedBy)
                VALUES (@UserId, @RoleId, @BranchId, @DepartmentId, SYSUTCDATETIME(), @AssignedBy)
                """,
                new
                {
                    UserId = userId,
                    RoleId = roleId,
                    BranchId = branchId,
                    DepartmentId = departmentId,
                    AssignedBy = assignedBy
                },
                cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)
                """, new { UserId = userId, RoleId = roleId }, cancellationToken: cancellationToken));
        }
    }

    public static AssignedRoleDto ToDto(AssignedRoleRow row) => new(
        row.RoleId,
        row.Code,
        row.Name,
        string.IsNullOrWhiteSpace(row.DisplayName) ? row.Name : row.DisplayName!,
        row.Category,
        row.RoleType,
        row.BranchId,
        row.DepartmentId);
}
