using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Ensures tenants with the AI module have a usable DeepInfra provider row.
/// Empty AiProviderConfig (defaults Provider=None) makes SheikhGo AI Assistant fall back to
/// keyword-only / help replies even when DEEPINFRA_API_KEY is configured on the host.
/// </summary>
public static class AiProviderDeepInfraSeedMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'AiProviderConfig', N'U') IS NULL RETURN;
            IF OBJECT_ID(N'TenantModules', N'U') IS NULL RETURN;
            IF OBJECT_ID(N'Modules', N'U') IS NULL RETURN;

            ;WITH AiTenants AS (
                SELECT DISTINCT tm.TenantId
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE m.ModuleCode = N'AI'
            )
            MERGE AiProviderConfig AS t
            USING AiTenants AS s ON t.TenantId = s.TenantId
            WHEN MATCHED AND (
                    t.Provider IS NULL
                    OR LTRIM(RTRIM(t.Provider)) = N''
                    OR t.Provider = N'None'
                    OR t.IsEnabled = 0
                    OR t.CopilotEnabled = 0
                ) THEN
                UPDATE SET
                    Provider = N'DeepInfra',
                    IsEnabled = 1,
                    CopilotEnabled = 1,
                    ModelName = COALESCE(NULLIF(LTRIM(RTRIM(t.ModelName)), N''), N'Qwen/Qwen3-32B'),
                    UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (
                    TenantId, Provider, IsEnabled, CopilotEnabled,
                    DecisionEngineEnabled, DigestEnabled, PredictionsEnabled,
                    ModelName, UpdatedAt)
                VALUES (
                    s.TenantId, N'DeepInfra', 1, 1,
                    1, 1, 1,
                    N'Qwen/Qwen3-32B', SYSUTCDATETIME());
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "AiProviderDeepInfraSeedMigration applied (MERGE for AI-module tenants). RowsAffected={Rows}",
            affected);
    }
}
