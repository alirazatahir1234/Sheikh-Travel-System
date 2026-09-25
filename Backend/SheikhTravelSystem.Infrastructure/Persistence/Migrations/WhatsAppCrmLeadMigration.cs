using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Extends WebsiteContactRequests for WhatsApp CRM leads (Source, DEMO fields, WA FKs).
/// Does not create a parallel Lead table.
/// </summary>
public static class WhatsAppCrmLeadMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'WebsiteContactRequests', N'U') IS NULL
                THROW 50001, 'WebsiteContactRequests missing — run WebsiteCmsMigration first.', 1;
            """, cancellationToken: cancellationToken));

        await AddColumnIfMissingAsync(connection, "Source", "NVARCHAR(40) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "FleetType", "NVARCHAR(120) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "MainChallenge", "NVARCHAR(120) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "CurrentSystem", "NVARCHAR(160) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "WhatsAppConversationId", "INT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "WhatsAppAccountId", "INT NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteContactRequests
            SET Source = N'Website'
            WHERE Source IS NULL OR LTRIM(RTRIM(Source)) = N'';
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'WebsiteContactRequests', N'Source') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.default_constraints dc
                   JOIN sys.columns c ON c.default_object_id = dc.object_id
                   WHERE dc.parent_object_id = OBJECT_ID(N'WebsiteContactRequests')
                     AND c.name = N'Source')
            BEGIN
                ALTER TABLE WebsiteContactRequests
                    ADD CONSTRAINT DF_WebsiteContact_Source DEFAULT N'Website' FOR Source;
            END
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_WebsiteContact_Source' AND object_id = OBJECT_ID(N'WebsiteContactRequests'))
            BEGIN
                CREATE INDEX IX_WebsiteContact_Source
                    ON WebsiteContactRequests(TenantId, Source, CreatedAt DESC);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("WhatsAppCrmLeadMigration applied successfully.");
    }

    private static async Task AddColumnIfMissingAsync(
        IDbConnection connection, string column, string sqlType, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF COL_LENGTH(N'WebsiteContactRequests', N'{column}') IS NULL
                ALTER TABLE [WebsiteContactRequests] ADD [{column}] {sqlType};
            """, cancellationToken: ct));
    }
}
