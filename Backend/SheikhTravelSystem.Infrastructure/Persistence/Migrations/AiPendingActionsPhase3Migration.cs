using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>Phase 3 — server-side pending write actions for AI confirm flow.</summary>
public static class AiPendingActionsPhase3Migration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.AiPendingActions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AiPendingActions (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiPendingActions PRIMARY KEY,
                    SessionId UNIQUEIDENTIFIER NOT NULL,
                    TenantId INT NOT NULL,
                    UserId INT NOT NULL,
                    ToolName NVARCHAR(50) NOT NULL,
                    ArgsJson NVARCHAR(MAX) NOT NULL,
                    Summary NVARCHAR(500) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AiPendingActions_CreatedAt DEFAULT (GETUTCDATE()),
                    ExpiresAt DATETIME2 NOT NULL,
                    CONSTRAINT FK_AiPendingActions_Sessions FOREIGN KEY (SessionId)
                        REFERENCES dbo.AiChatSessions (Id)
                );
                CREATE UNIQUE INDEX UX_AiPendingActions_Session ON dbo.AiPendingActions (SessionId);
                CREATE INDEX IX_AiPendingActions_Expires ON dbo.AiPendingActions (ExpiresAt);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("AiPendingActionsPhase3Migration applied.");
    }
}
