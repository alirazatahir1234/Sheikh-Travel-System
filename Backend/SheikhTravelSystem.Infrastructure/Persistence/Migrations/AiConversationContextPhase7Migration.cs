using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 7 — conversation context summary columns on AiChatSessions (bounded memory).
/// </summary>
public static class AiConversationContextPhase7Migration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.AiChatSessions', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.AiChatSessions', N'ContextSummary') IS NULL
                    ALTER TABLE dbo.AiChatSessions ADD ContextSummary NVARCHAR(MAX) NULL;

                IF COL_LENGTH(N'dbo.AiChatSessions', N'SummaryUpdatedAt') IS NULL
                    ALTER TABLE dbo.AiChatSessions ADD SummaryUpdatedAt DATETIME2 NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("AiConversationContextPhase7Migration applied.");
    }
}
