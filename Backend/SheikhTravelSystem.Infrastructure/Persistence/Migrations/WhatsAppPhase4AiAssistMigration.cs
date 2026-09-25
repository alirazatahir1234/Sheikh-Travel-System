using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 4.1: WhatsApp.AiAssist permission + lightweight AI assist audit log.
/// Does not store full conversation content or provider secrets.
/// </summary>
public static class WhatsAppPhase4AiAssistMigration
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
            IF OBJECT_ID(N'WhatsAppAiAssistLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE WhatsAppAiAssistLogs (
                    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL,
                    ConversationId INT NOT NULL,
                    UserId INT NULL,
                    Operation NVARCHAR(40) NOT NULL,
                    Provider NVARCHAR(80) NULL,
                    Model NVARCHAR(120) NULL,
                    DurationMs INT NOT NULL CONSTRAINT DF_WhatsAppAiAssistLogs_DurationMs DEFAULT 0,
                    Success BIT NOT NULL,
                    Confidence NVARCHAR(20) NULL,
                    ErrorCode NVARCHAR(80) NULL,
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppAiAssistLogs_CreatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE INDEX IX_WhatsAppAiAssistLogs_Tenant_Conv
                    ON WhatsAppAiAssistLogs(TenantId, ConversationId, CreatedAtUtc DESC);
            END
            """, cancellationToken: cancellationToken));

        await SeedPermissionsAsync(connection, cancellationToken);
        logger.LogInformation("WhatsAppPhase4AiAssistMigration applied successfully.");
    }

    private static async Task SeedPermissionsAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: ct)) != 1)
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (N'WhatsApp', @Code, @Desc);
            """, new
            {
                Code = WhatsAppPermissions.AiAssist,
                Desc = "Use AI reply suggestions and summaries in WhatsApp inbox"
            }, cancellationToken: ct));

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", WhatsAppPermissions.All, ct);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", WhatsAppPermissions.All, ct);
    }
}
