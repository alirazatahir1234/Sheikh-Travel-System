using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

public class TenantRoleSeedService(IDbConnectionFactory dbFactory) : ITenantRoleSeedService
{
    public async Task SeedSystemRolePermissionsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await TenantRolePermissionSeeder.SeedSystemRolePermissionsForTenantAsync(
            connection, tenantId, cancellationToken);
    }

    public async Task SeedSuperAdminPermissionsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId != 1) return;

        using var connection = dbFactory.CreateConnection();
        var codes = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PermissionCode FROM Permissions", cancellationToken: cancellationToken))).ToList();

        await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
            connection, tenantId, "SUPER_ADMIN", codes, cancellationToken);
    }
}
