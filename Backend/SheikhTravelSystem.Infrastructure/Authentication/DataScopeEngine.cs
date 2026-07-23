using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

/// <summary>
/// Stage 12 Data Scope Engine: Users home org + UserRoles soft scopes → effective clamp.
/// </summary>
public class DataScopeEngine(IDbConnectionFactory dbFactory) : IDataScopeEngine
{
    public async Task<DataScopeResult> ResolveAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var home = await connection.QuerySingleOrDefaultAsync<(int? BranchId, int? DepartmentId)>(
            new CommandDefinition("""
                SELECT BranchId, DepartmentId
                FROM Users
                WHERE Id = @UserId AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new { UserId = userId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        List<DataScopeResolver.RoleAssignmentInput> assignments;
        try
        {
            var rows = await connection.QueryAsync<(string Code, string? ScopeLevel, int? BranchId, int? DepartmentId)>(
                new CommandDefinition("""
                    SELECT r.Code,
                           r.ScopeLevel,
                           ur.BranchId,
                           ur.DepartmentId
                    FROM UserRoles ur
                    INNER JOIN Roles r ON r.Id = ur.RoleId AND r.IsActive = 1
                    WHERE ur.UserId = @UserId AND r.TenantId = @TenantId
                    ORDER BY r.SortOrder, r.Code
                    """,
                    new { UserId = userId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            assignments = rows
                .Select(r => new DataScopeResolver.RoleAssignmentInput(
                    r.Code,
                    r.ScopeLevel,
                    r.BranchId,
                    r.DepartmentId))
                .ToList();
        }
        catch
        {
            // Pre-migration: ScopeLevel column may be missing.
            var rows = await connection.QueryAsync<(string Code, int? BranchId, int? DepartmentId)>(
                new CommandDefinition("""
                    SELECT r.Code, ur.BranchId, ur.DepartmentId
                    FROM UserRoles ur
                    INNER JOIN Roles r ON r.Id = ur.RoleId AND r.IsActive = 1
                    WHERE ur.UserId = @UserId AND r.TenantId = @TenantId
                    ORDER BY r.Code
                    """,
                    new { UserId = userId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            assignments = rows
                .Select(r => new DataScopeResolver.RoleAssignmentInput(
                    r.Code,
                    DataScopeResolver.DefaultScopeLevelForRoleCode(r.Code),
                    r.BranchId,
                    r.DepartmentId))
                .ToList();
        }

        return DataScopeResolver.Resolve(
            userId,
            tenantId,
            home.BranchId,
            home.DepartmentId,
            assignments);
    }
}
