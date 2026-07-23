using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Authentication;

public class UserAccessService(IDbConnectionFactory dbFactory) : IUserAccessService
{
    public async Task<UserAccessContext> ResolveAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var roleCodes = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT r.Code
            FROM UserRoles ur
            INNER JOIN Roles r ON r.Id = ur.RoleId AND r.IsActive = 1
            WHERE ur.UserId = @UserId AND r.TenantId = @TenantId
            ORDER BY r.Code
            """, new { UserId = userId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        var legacyRole = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Role FROM Users WHERE Id = @UserId AND IsDeleted = 0",
            new { UserId = userId },
            cancellationToken: cancellationToken));

        if (roleCodes.Count == 0 && legacyRole.HasValue)
        {
            roleCodes.Add(MapLegacyRole((UserRole)legacyRole.Value));
        }
        else if (legacyRole.HasValue)
        {
            // Bridge legacy Admin → TENANT_ADMIN even when UserRoles already exist.
            EnsureLegacyBridge(roleCodes, (UserRole)legacyRole.Value);
        }

        if (roleCodes.Contains(PlatformRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            var allPermissions = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT PermissionCode FROM Permissions ORDER BY PermissionCode",
                cancellationToken: cancellationToken))).ToList();
            return new UserAccessContext(userId, tenantId, roleCodes, allPermissions);
        }

        var permissions = roleCodes.Count == 0
            ? (IReadOnlyList<string>)[]
            : (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT DISTINCT p.PermissionCode
                FROM Roles r
                INNER JOIN RolePermissions rp ON rp.RoleId = r.Id
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE r.TenantId = @TenantId AND r.IsActive = 1 AND r.Code IN @RoleCodes
                ORDER BY p.PermissionCode
                """, new { TenantId = tenantId, RoleCodes = roleCodes }, cancellationToken: cancellationToken))).ToList();

        // If RolePermissions rows are missing (fresh DB / partial seed), fall back to templates.
        if (permissions.Count == 0 && roleCodes.Count > 0)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in roleCodes)
            {
                var match = TenantRolePermissionTemplates.StandardRoles
                    .FirstOrDefault(r => string.Equals(r.RoleCode, code, StringComparison.OrdinalIgnoreCase));
                if (match.Permissions is { Length: > 0 } perms)
                {
                    foreach (var p in perms)
                        set.Add(p);
                }
            }

            permissions = set.OrderBy(p => p).ToList();
        }

        return new UserAccessContext(userId, tenantId, roleCodes, permissions);
    }

    private static void EnsureLegacyBridge(List<string> roleCodes, UserRole legacyRole)
    {
        var mapped = MapLegacyRole(legacyRole);
        if (!roleCodes.Contains(mapped, StringComparer.OrdinalIgnoreCase))
            roleCodes.Add(mapped);
    }

    private static string MapLegacyRole(UserRole role) => role switch
    {
        UserRole.Admin => PlatformRoles.TenantAdmin,
        UserRole.Dispatcher => "DISPATCHER",
        UserRole.Driver => "DRIVER",
        UserRole.Accountant => "ACCOUNTANT",
        _ => PlatformRoles.TenantAdmin
    };
}
