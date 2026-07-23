using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 8 Permission Engine: catalog metadata on Permissions.
/// </summary>
public static class PermissionEngineFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "Permissions", "DisplayName", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Permissions", "Category", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Permissions", "SortOrder", "INT NOT NULL CONSTRAINT DF_Permissions_SortOrder DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Permissions", "Visible", "BIT NOT NULL CONSTRAINT DF_Permissions_Visible DEFAULT (1)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Permissions", "Action", "NVARCHAR(50) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Permissions", "ModuleKey", "NVARCHAR(100) NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Permissions
            SET DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), Description, PermissionCode)
            WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = '';
            """, cancellationToken: cancellationToken));

        foreach (var row in PermissionRegistrySeed.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Permissions SET
                    DisplayName = @DisplayName,
                    Category = @Category,
                    SortOrder = @SortOrder,
                    Visible = @Visible,
                    Action = @Action,
                    ModuleKey = @ModuleKey
                WHERE PermissionCode = @PermissionCode;
                """,
                new
                {
                    row.PermissionCode,
                    row.DisplayName,
                    row.Category,
                    row.SortOrder,
                    Visible = row.Visible,
                    row.Action,
                    row.ModuleKey
                },
                cancellationToken: cancellationToken));
        }

        // Derive Action for any leftover catalog rows not in seed.
        var leftovers = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PermissionCode FROM Permissions WHERE Action IS NULL OR LTRIM(RTRIM(Action)) = ''",
            cancellationToken: cancellationToken))).ToList();

        foreach (var leftoverCode in leftovers)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Permissions SET
                    Action = @Action,
                    DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), @DisplayName),
                    Category = COALESCE(NULLIF(LTRIM(RTRIM(Category)), ''), N'Other')
                WHERE PermissionCode = @PermissionCode
                """,
                new
                {
                    PermissionCode = leftoverCode,
                    Action = PermissionRegistrySeed.DeriveAction(leftoverCode),
                    DisplayName = PermissionRegistrySeed.DeriveDisplayName(leftoverCode)
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "PermissionEngineFoundationMigration applied ({Count} seed metadata rows on Permissions).",
            PermissionRegistrySeed.All.Count);
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
