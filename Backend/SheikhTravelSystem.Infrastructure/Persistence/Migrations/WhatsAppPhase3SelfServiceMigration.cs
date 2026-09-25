using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>Phase 3 self-service: bot session JSON + Flow submission idempotency.</summary>
public static class WhatsAppPhase3SelfServiceMigration
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
            IF COL_LENGTH('WhatsAppConversations', 'BotSessionJson') IS NULL
                ALTER TABLE WhatsAppConversations ADD BotSessionJson NVARCHAR(MAX) NULL;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.WhatsAppFlowSubmissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.WhatsAppFlowSubmissions
                (
                    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WhatsAppFlowSubmissions PRIMARY KEY,
                    TenantId        INT NOT NULL,
                    ConversationId  INT NOT NULL,
                    IdempotencyKey  NVARCHAR(128) NOT NULL,
                    BookingId       INT NULL,
                    CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppFlowSubmissions_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_WhatsAppFlowSubmissions_Tenant_Key UNIQUE (TenantId, IdempotencyKey)
                );
                CREATE INDEX IX_WhatsAppFlowSubmissions_Conversation
                    ON dbo.WhatsAppFlowSubmissions (TenantId, ConversationId);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("WhatsAppPhase3SelfServiceMigration applied.");
    }
}
