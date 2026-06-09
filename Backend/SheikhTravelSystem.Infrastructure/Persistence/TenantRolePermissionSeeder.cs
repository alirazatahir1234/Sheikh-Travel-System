using Dapper;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Shared SQL helpers for seeding RolePermissions — used by migrations and provisioning.
/// </summary>
internal static class TenantRolePermissionSeeder
{
    public static async Task AssignRolePermissionsForTenantAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        string roleCode,
        IEnumerable<string> permissionCodes,
        CancellationToken ct,
        System.Data.IDbTransaction? transaction = null)
    {
        foreach (var permCode in permissionCodes)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id
                FROM Roles r
                INNER JOIN Permissions p ON p.PermissionCode = @PermCode
                WHERE r.TenantId = @TenantId AND r.Code = @RoleCode
                  AND NOT EXISTS (
                    SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
                  );
                """, new { TenantId = tenantId, RoleCode = roleCode, PermCode = permCode },
                transaction: transaction,
                cancellationToken: ct));
        }
    }

    public static async Task SeedSystemRolePermissionsForTenantAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        CancellationToken ct,
        System.Data.IDbTransaction? transaction = null)
    {
        foreach (var (roleCode, permissions) in Application.Common.TenantRolePermissionTemplates.StandardRoles)
        {
            await AssignRolePermissionsForTenantAsync(
                connection, tenantId, roleCode, permissions, ct, transaction);
        }
    }

    public static async Task AssignRolePermissionsForAllTenantsAsync(
        System.Data.IDbConnection connection,
        string roleCode,
        IEnumerable<string> permissionCodes,
        CancellationToken ct)
    {
        foreach (var permCode in permissionCodes)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id
                FROM Roles r
                CROSS JOIN Permissions p
                WHERE r.Code = @RoleCode AND p.PermissionCode = @PermCode
                  AND NOT EXISTS (
                    SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
                  );
                """, new { RoleCode = roleCode, PermCode = permCode }, cancellationToken: ct));
        }
    }
}
