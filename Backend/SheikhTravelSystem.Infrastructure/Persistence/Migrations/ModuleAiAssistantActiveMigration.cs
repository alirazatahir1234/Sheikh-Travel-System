using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Text.Json;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Promotes AI (SheikhGo AI Assistant) from Beta → Active enableable module with legacy key "ai".
/// Required so company create / module assignment can toggle SheikhGo AI Assistant (opt-in).
/// </summary>
public static class ModuleAiAssistantActiveMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID('Modules', 'U') IS NULL RETURN;

            IF EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = N'AI')
                UPDATE Modules SET
                    ModuleName = N'SheikhGo AI Assistant',
                    DisplayName = N'SheikhGo AI Assistant',
                    Description = N'Premium SheikhGo AI Assistant for operations chat and tools',
                    Category = N'Platform',
                    Icon = N'smart_toy',
                    Route = N'/ai',
                    SortOrder = 120,
                    DependenciesJson = N'["DASHBOARD"]',
                    Visible = 1,
                    IsMobileSupported = 1,
                    IsAISupported = 1,
                    IsGPSSupported = 0,
                    Status = N'Active',
                    LegacyKeysJson = N'["ai"]'
                WHERE ModuleCode = N'AI';
            ELSE
                INSERT INTO Modules (
                    ModuleCode, ModuleName, DisplayName, Description, Category,
                    Version, Icon, Route, SortOrder, DependenciesJson, Visible,
                    IsMobileSupported, IsAISupported, IsGPSSupported, Status, LegacyKeysJson)
                VALUES (
                    N'AI', N'SheikhGo AI Assistant', N'SheikhGo AI Assistant',
                    N'Premium SheikhGo AI Assistant for operations chat and tools', N'Platform',
                    N'1.0.0', N'smart_toy', N'/ai', 120, N'["DASHBOARD"]', 1,
                    1, 1, 0, N'Active', N'["ai"]');
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "ModuleAiAssistantActiveMigration applied (AI → Active enableable, legacy key ai). Keys={Keys}",
            JsonSerializer.Serialize(new[] { "ai" }));
    }
}
