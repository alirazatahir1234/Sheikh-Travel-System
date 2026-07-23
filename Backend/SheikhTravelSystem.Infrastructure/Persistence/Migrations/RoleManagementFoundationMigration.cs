using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 7 Role Management: business-role metadata on Roles + soft scope/audit on UserRoles.
/// </summary>
public static class RoleManagementFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "Roles", "DisplayName", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Roles", "Description", "NVARCHAR(500) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Roles", "Category", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Roles", "SortOrder", "INT NOT NULL CONSTRAINT DF_Roles_SortOrder DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Roles", "Visible", "BIT NOT NULL CONSTRAINT DF_Roles_Visible DEFAULT (1)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Roles", "RoleType", "NVARCHAR(50) NOT NULL CONSTRAINT DF_Roles_RoleType DEFAULT N'Custom'", cancellationToken);

        await AddColumnIfMissingAsync(connection, "UserRoles", "BranchId", "INT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "UserRoles", "DepartmentId", "INT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "UserRoles", "AssignedAt", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "UserRoles", "AssignedBy", "INT NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Roles SET DisplayName = Name WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = '';
            UPDATE Roles SET RoleType = CASE WHEN IsSystem = 1 THEN N'System' ELSE N'Custom' END
            WHERE RoleType IS NULL OR RoleType = N'' OR (IsSystem = 1 AND RoleType <> N'System');
            """, cancellationToken: cancellationToken));

        foreach (var row in RoleRegistrySeed.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Roles SET
                    Name = @Name,
                    DisplayName = @DisplayName,
                    Description = @Description,
                    Category = @Category,
                    SortOrder = @SortOrder,
                    Visible = 1,
                    RoleType = @RoleType
                WHERE Code = @Code;
                """,
                new
                {
                    row.Code,
                    row.Name,
                    row.DisplayName,
                    row.Description,
                    row.Category,
                    row.SortOrder,
                    row.RoleType
                },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE UserRoles
            SET AssignedAt = COALESCE(AssignedAt, SYSUTCDATETIME())
            WHERE AssignedAt IS NULL;
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "RoleManagementFoundationMigration applied ({RoleCount} system role metadata rows + UserRoles scope columns).",
            RoleRegistrySeed.All.Count);
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
