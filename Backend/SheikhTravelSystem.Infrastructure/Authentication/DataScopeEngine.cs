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

        // SG-05 A7: Platform.Tenants.DataRead + working tenant ≠ home → company-wide (source platform).
        var homeTenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TenantId FROM Users WHERE Id = @UserId AND IsDeleted = 0
            """, new { UserId = userId }, cancellationToken: cancellationToken));

        var hasDataRead = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 1
            FROM UserRoles ur
            INNER JOIN Roles r ON r.Id = ur.RoleId AND r.IsActive = 1 AND r.TenantId = @PlatformTenantId
            INNER JOIN RolePermissions rp ON rp.RoleId = r.Id
            INNER JOIN Permissions p ON p.Id = rp.PermissionId
            WHERE ur.UserId = @UserId AND p.PermissionCode = @DataRead
            """,
            new
            {
                UserId = userId,
                PlatformTenantId = PlatformTenant.Id,
                DataRead = PlatformPermissions.TenantsDataRead
            },
            cancellationToken: cancellationToken)) == 1;

        if (hasDataRead && homeTenantId.HasValue && tenantId != homeTenantId.Value)
        {
            return DataScopeResolver.ResolvePlatformCrossTenant(
                userId,
                tenantId,
                home.BranchId,
                home.DepartmentId);
        }

        return DataScopeResolver.Resolve(
            userId,
            tenantId,
            home.BranchId,
            home.DepartmentId,
            assignments);
    }
}
