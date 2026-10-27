using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 6 Logs — structured MCP tool execution ledger (tenant-scoped).
/// Written by SheikhGoMcpBridge; queried via MCP search_mcp_executions.
/// Args/errors must be sanitized at write time — never store secrets.
/// </summary>
public static class McpToolExecutionsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.McpToolExecutions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.McpToolExecutions (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_McpToolExecutions PRIMARY KEY,
                    TenantId INT NOT NULL,
                    ToolName NVARCHAR(128) NOT NULL,
                    Success BIT NOT NULL,
                    DurationMs INT NOT NULL CONSTRAINT DF_McpToolExecutions_DurationMs DEFAULT (0),
                    CorrelationId NVARCHAR(64) NULL,
                    SessionId UNIQUEIDENTIFIER NULL,
                    UserId INT NULL,
                    ArgsSanitized NVARCHAR(1000) NULL,
                    ErrorSanitized NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_McpToolExecutions_CreatedAt DEFAULT (GETUTCDATE())
                );

                CREATE INDEX IX_McpToolExecutions_Tenant_Created
                    ON dbo.McpToolExecutions (TenantId, CreatedAt DESC);

                CREATE INDEX IX_McpToolExecutions_Tenant_Tool_Created
                    ON dbo.McpToolExecutions (TenantId, ToolName, CreatedAt DESC);

                CREATE INDEX IX_McpToolExecutions_Tenant_Correlation
                    ON dbo.McpToolExecutions (TenantId, CorrelationId)
                    WHERE CorrelationId IS NOT NULL;

                CREATE INDEX IX_McpToolExecutions_Tenant_Session
                    ON dbo.McpToolExecutions (TenantId, SessionId)
                    WHERE SessionId IS NOT NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("McpToolExecutionsMigration applied.");
    }
}
