using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 6 User Management: profile metadata, lifecycle Status, workspace defaults on Users.
/// Does not change authentication / JWT.
/// </summary>
public static class UserManagementFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "Users", "JobTitle", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "EmployeeCode", "NVARCHAR(50) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "EmployeeType", "NVARCHAR(50) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "Status", "NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_Status DEFAULT N'Active'", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "DefaultWorkspaceKey", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "DefaultDashboardKey", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "HomeRoute", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "TimeZone", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "Language", "NVARCHAR(20) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "Theme", "NVARCHAR(50) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "AvatarUrl", "NVARCHAR(500) NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users
            SET Status = CASE WHEN IsActive = 1 THEN N'Active' ELSE N'Inactive' END
            WHERE Status IS NULL OR Status = N'' OR (IsActive = 1 AND Status = N'Inactive') OR (IsActive = 0 AND Status = N'Active');
            """, cancellationToken: cancellationToken));

        // Prefer Status-driven sync once: Active users stay Active when IsActive=1.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users
            SET Status = CASE WHEN IsActive = 1 THEN N'Active' ELSE
                CASE WHEN Status IN (N'Pending', N'Suspended', N'Locked', N'Inactive') THEN Status ELSE N'Inactive' END
            END
            WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        logger.LogInformation("UserManagementFoundationMigration applied (Users profile/lifecycle/workspace columns).");
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
