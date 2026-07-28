using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds GPS_OPERATOR role + permission template for all tenants (mobile GPS Operator V1).
/// </summary>
public static class GpsOperatorRoleTemplateMigration
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
                SELECT t.Id, N'GPS Operator', N'GPS_OPERATOR', 1, 1, GETUTCDATE()
                FROM Tenants t
                WHERE NOT EXISTS (
                    SELECT 1 FROM Roles r
                    WHERE r.TenantId = t.Id AND r.Code = N'GPS_OPERATOR'
                );
            END
            """, cancellationToken: cancellationToken));

        // Optional Roles columns — dynamic SQL so SQL Server does not compile missing column names.
        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "DisplayName",
            "UPDATE Roles SET DisplayName = N'GPS Operator' WHERE Code = N'GPS_OPERATOR' AND (DisplayName IS NULL OR DisplayName = N'')",
            cancellationToken);
        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "Category",
            "UPDATE Roles SET Category = N'Fleet' WHERE Code = N'GPS_OPERATOR' AND (Category IS NULL OR Category = N'')",
            cancellationToken);
        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "DefaultWorkspaceKey",
            "UPDATE Roles SET DefaultWorkspaceKey = N'fleet' WHERE Code = N'GPS_OPERATOR' AND DefaultWorkspaceKey IS NULL",
            cancellationToken);
        await ApplyOptionalRoleColumnUpdateAsync(
            connection,
            "ScopeLevel",
            "UPDATE Roles SET ScopeLevel = N'Tenant' WHERE Code = N'GPS_OPERATOR' AND (ScopeLevel IS NULL OR ScopeLevel = N'')",
            cancellationToken);

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "GPS_OPERATOR",
            TenantRolePermissionTemplates.GpsOperator,
            cancellationToken);

        logger.LogInformation(
            "GpsOperatorRoleTemplateMigration applied (GPS_OPERATOR template for all tenants).");
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
