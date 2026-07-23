using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>Phase 1 AI Gateway — chat sessions + message memory for Ollama/Mistral copilot.</summary>
public static class AiChatPhase1Migration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.AiChatSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AiChatSessions (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiChatSessions PRIMARY KEY,
                    TenantId INT NOT NULL,
                    UserId INT NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AiChatSessions_CreatedAt DEFAULT (GETUTCDATE()),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_AiChatSessions_UpdatedAt DEFAULT (GETUTCDATE()),
                    IsDeleted BIT NOT NULL CONSTRAINT DF_AiChatSessions_IsDeleted DEFAULT (0)
                );
                CREATE INDEX IX_AiChatSessions_Tenant_User ON dbo.AiChatSessions (TenantId, UserId, UpdatedAt DESC);
            END

            IF OBJECT_ID(N'dbo.AiChatMessages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AiChatMessages (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AiChatMessages PRIMARY KEY,
                    SessionId UNIQUEIDENTIFIER NOT NULL,
                    Role NVARCHAR(20) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AiChatMessages_CreatedAt DEFAULT (GETUTCDATE()),
                    CONSTRAINT FK_AiChatMessages_Sessions FOREIGN KEY (SessionId)
                        REFERENCES dbo.AiChatSessions (Id)
                );
                CREATE INDEX IX_AiChatMessages_Session ON dbo.AiChatMessages (SessionId, Id);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("AiChatPhase1Migration applied.");
    }
}
