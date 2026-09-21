using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds fine-grained Ai.* Operations permissions and assigns them to role templates.
/// Idempotent. Legacy Ai.View / Ai.Manage remain and still authorize via aliases.
/// </summary>
public static class AiOperationsPermissionsMigration
{
    private static readonly (string Module, string Code, string Desc)[] Permissions =
    [
        ("AI", AiPermissions.Chat, "Use AI chat / copilot"),
        ("AI", AiPermissions.ViewPredictions, "View AI predictions"),
        ("AI", AiPermissions.RunPredictions, "Run AI prediction pipeline"),
        ("AI", AiPermissions.ViewRecommendations, "View AI recommendations"),
        ("AI", AiPermissions.RefreshRecommendations, "Refresh AI recommendations"),
        ("AI", AiPermissions.ViewProviderHealth, "View AI provider health (admin)"),
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        foreach (var (module, code, desc) in Permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (@Module, @Code, @Desc);
                """, new { Module = module, Code = code, Desc = desc },
                cancellationToken: cancellationToken));
        }

        var adminCodes = Permissions.Select(p => p.Code).ToArray();
        var dispatcherCodes = new[]
        {
            AiPermissions.Chat,
            AiPermissions.ViewPredictions,
            AiPermissions.ViewRecommendations
        };

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", adminCodes, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", adminCodes, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "FLEET_MANAGER", adminCodes, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DISPATCHER", dispatcherCodes, cancellationToken);

        logger.LogInformation(
            "AiOperationsPermissionsMigration applied (fine-grained Ai.* Operations permissions).");
    }
}
