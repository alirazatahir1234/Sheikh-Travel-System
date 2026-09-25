using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds LastHealth* columns on WhatsAppAccounts for admin health checks.
/// </summary>
public static class WhatsAppAccountAdminMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "LastHealthCheckedAtUtc", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "LastHealthStatus", "NVARCHAR(40) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccounts", "LastHealthMessage", "NVARCHAR(500) NULL", cancellationToken);
        logger.LogInformation("WhatsAppAccountAdminMigration applied successfully.");
    }

    private static async Task AddColumnIfMissingAsync(
        IDbConnection connection, string table, string column, string sqlType, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF COL_LENGTH(N'{table}', N'{column}') IS NULL
                ALTER TABLE [{table}] ADD [{column}] {sqlType};
            """, cancellationToken: ct));
    }
}
