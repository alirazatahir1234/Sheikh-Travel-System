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

        if (roleCodes.Count == 0)
        {
            var legacyRole = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Role FROM Users WHERE Id = @UserId AND IsDeleted = 0",
                new { UserId = userId },
                cancellationToken: cancellationToken));

            if (legacyRole.HasValue)
            {
                roleCodes.Add(MapLegacyRole((UserRole)legacyRole.Value));
            }
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
                FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId AND r.TenantId = @TenantId
                INNER JOIN RolePermissions rp ON rp.RoleId = r.Id
                INNER JOIN Permissions p ON p.Id = rp.PermissionId
                WHERE ur.UserId = @UserId
                ORDER BY p.PermissionCode
                """, new { UserId = userId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        return new UserAccessContext(userId, tenantId, roleCodes, permissions);
    }

    private static string MapLegacyRole(UserRole role) => role switch
    {
        UserRole.Admin => "TENANT_ADMIN",
        UserRole.Dispatcher => "DISPATCHER",
        UserRole.Driver => "DRIVER",
        UserRole.Accountant => "ACCOUNTANT",
        _ => "TENANT_ADMIN"
    };
}
