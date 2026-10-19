using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds OWNER role + permission template for all tenants (SheikhGo Tracking Owner mobile).
/// </summary>
public static class OwnerRoleTemplateMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Roles')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Tenants')
            BEGIN
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                SELECT t.Id, N'Owner', N'OWNER', 1, 1, GETUTCDATE()
                FROM Tenants t
                WHERE NOT EXISTS (
                    SELECT 1 FROM Roles r
                    WHERE r.TenantId = t.Id AND r.Code = N'OWNER'
                );
            END
            """, cancellationToken: cancellationToken));

        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "DisplayName",
            "UPDATE Roles SET DisplayName = N'Owner' WHERE Code = N'OWNER' AND (DisplayName IS NULL OR DisplayName = N'')",
            cancellationToken);
        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "Category",
            "UPDATE Roles SET Category = N'Fleet' WHERE Code = N'OWNER' AND (Category IS NULL OR Category = N'')",
            cancellationToken);
        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "DefaultWorkspaceKey",
            "UPDATE Roles SET DefaultWorkspaceKey = N'fleet' WHERE Code = N'OWNER' AND DefaultWorkspaceKey IS NULL",
            cancellationToken);
        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "ScopeLevel",
            "UPDATE Roles SET ScopeLevel = N'Company' WHERE Code = N'OWNER' AND (ScopeLevel IS NULL OR ScopeLevel = N'' OR ScopeLevel = N'Tenant')",
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "OWNER",
            TenantRolePermissionTemplates.Owner,
            cancellationToken);

        logger.LogInformation(
            "OwnerRoleTemplateMigration applied (OWNER template for all tenants).");
    }

    private static async Task ApplyOptionalRoleColumnUpdateAsync(
        System.Data.IDbConnection connection,
        string columnName,
        string updateSql,
        CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN COL_LENGTH('Roles', @Column) IS NOT NULL THEN 1 ELSE 0 END",
            new { Column = columnName },
            cancellationToken: cancellationToken));
        if (exists == 0) return;
        await connection.ExecuteAsync(new CommandDefinition(updateSql, cancellationToken: cancellationToken));
    }
}
