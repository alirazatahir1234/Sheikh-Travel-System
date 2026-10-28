using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Persist structured assistant widgets (KPIs/charts) alongside AiChatMessages text.
/// Nullable columns — existing text-only rows remain valid.
/// </summary>
public static class AiChatMessagePayloadMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.AiChatMessages', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.AiChatMessages', N'StructuredPayload') IS NULL
                    ALTER TABLE dbo.AiChatMessages ADD StructuredPayload NVARCHAR(MAX) NULL;

                IF COL_LENGTH(N'dbo.AiChatMessages', N'ToolUsed') IS NULL
                    ALTER TABLE dbo.AiChatMessages ADD ToolUsed NVARCHAR(120) NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("AiChatMessagePayloadMigration applied.");
    }
}
