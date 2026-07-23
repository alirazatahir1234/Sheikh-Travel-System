using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 12 Data Scope: Roles.ScopeLevel metadata for company/branch/department intent.
/// </summary>
public static class DataScopeFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(
            connection,
            "Roles",
            "ScopeLevel",
            "NVARCHAR(40) NULL",
            cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Roles SET ScopeLevel = N'Company'
            WHERE Code IN (N'SUPER_ADMIN', N'TENANT_ADMIN')
              AND (ScopeLevel IS NULL OR LTRIM(RTRIM(ScopeLevel)) = '');

            UPDATE Roles SET ScopeLevel = N'Branch'
            WHERE Code IN (N'BRANCH_MANAGER', N'FLEET_MANAGER', N'DRIVER_MANAGER', N'DISPATCHER')
              AND (ScopeLevel IS NULL OR LTRIM(RTRIM(ScopeLevel)) = '');

            UPDATE Roles SET ScopeLevel = N'Assigned'
            WHERE ScopeLevel IS NULL OR LTRIM(RTRIM(ScopeLevel)) = '';
            """, cancellationToken: cancellationToken));

        logger.LogInformation("DataScopeFoundationMigration applied (Roles.ScopeLevel).");
    }

    private static async Task AddColumnIfMissingAsync(
        IDbConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column)
                ALTER TABLE [{table}] ADD [{column}] {definition};
            """,
            new { Table = table, Column = column },
            cancellationToken: cancellationToken));
    }
}
