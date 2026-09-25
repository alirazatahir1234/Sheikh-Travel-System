using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 1 gap closure: webhook ops log, WindowExpiresAt, AttemptCount.
/// Evolves existing WhatsApp* tables (not a wa.* schema rewrite).
/// </summary>
public static class WhatsAppPhase1GapMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        await EnsureWebhookLogsAsync(connection, cancellationToken);
        await EnsureConversationWindowExpiresAtAsync(connection, cancellationToken);
        await EnsureMessageAttemptCountAsync(connection, cancellationToken);
        logger.LogInformation("WhatsAppPhase1GapMigration applied successfully.");
    }

    private static async Task EnsureWebhookLogsAsync(IDbConnection connection, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, "WhatsAppWebhookLogs", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                CREATE TABLE WhatsAppWebhookLogs (
                    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WhatsAppWebhookLogs PRIMARY KEY,
                    ReceivedAtUtc   DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppWebhookLogs_ReceivedAtUtc DEFAULT (SYSUTCDATETIME()),
                    SignatureValid  BIT NOT NULL,
                    Payload         NVARCHAR(MAX) NOT NULL,
                    ProcessingStatus NVARCHAR(32) NOT NULL CONSTRAINT DF_WhatsAppWebhookLogs_Status DEFAULT (N'Received'),
                    Error           NVARCHAR(2000) NULL,
                    Attempts        INT NOT NULL CONSTRAINT DF_WhatsAppWebhookLogs_Attempts DEFAULT (0),
                    ProcessedAtUtc  DATETIME2 NULL,
                    PhoneNumberId   NVARCHAR(64) NULL,
                    TenantId        INT NULL
                );
                CREATE INDEX IX_WhatsAppWebhookLogs_ReceivedAtUtc ON WhatsAppWebhookLogs(ReceivedAtUtc DESC);
                CREATE INDEX IX_WhatsAppWebhookLogs_ProcessingStatus ON WhatsAppWebhookLogs(ProcessingStatus, ReceivedAtUtc DESC);
                """, cancellationToken: ct));
        }
    }

    private static async Task EnsureConversationWindowExpiresAtAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await TableExistsAsync(connection, "WhatsAppConversations", ct)
            && !await ColumnExistsAsync(connection, "WhatsAppConversations", "WindowExpiresAt", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                ALTER TABLE WhatsAppConversations ADD WindowExpiresAt DATETIME2 NULL;
                """, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppConversations
                SET WindowExpiresAt = DATEADD(HOUR, 24, LastIncomingMessageAt)
                WHERE LastIncomingMessageAt IS NOT NULL AND WindowExpiresAt IS NULL;
                """, cancellationToken: ct));
        }
    }

    private static async Task EnsureMessageAttemptCountAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await TableExistsAsync(connection, "WhatsAppMessages", ct)
            && !await ColumnExistsAsync(connection, "WhatsAppMessages", "AttemptCount", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                ALTER TABLE WhatsAppMessages ADD AttemptCount INT NOT NULL
                    CONSTRAINT DF_WhatsAppMessages_AttemptCount DEFAULT (1);
                """, cancellationToken: ct));
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        IDbConnection connection, string table, string column, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN COL_LENGTH(@Table, @Column) IS NULL THEN 0 ELSE 1 END
            """, new { Table = table, Column = column }, cancellationToken: ct));
        return n == 1;
    }

    private static async Task<bool> TableExistsAsync(IDbConnection connection, string table, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(@Table, N'U') IS NULL THEN 0 ELSE 1 END
            """, new { Table = table }, cancellationToken: ct));
        return n == 1;
    }
}
