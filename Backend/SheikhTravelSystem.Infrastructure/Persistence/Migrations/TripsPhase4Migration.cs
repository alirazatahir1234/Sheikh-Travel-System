using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Trips Phase 4: expanded lifecycle notification templates (delayed, cancelled, updated, arriving).
/// </summary>
public static class TripsPhase4Migration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var templates = new (string Key, string Name, string Channel)[]
        {
            ("trip_delayed", "Trip Delayed", "InApp"),
            ("trip_delayed", "Trip Delayed", "Email"),
            ("trip_cancelled", "Trip Cancelled", "InApp"),
            ("trip_cancelled", "Trip Cancelled", "Email"),
            ("trip_updated", "Trip Updated", "InApp"),
            ("trip_updated", "Trip Updated", "Email"),
            ("trip_driver_arriving", "Driver Arriving", "InApp"),
            ("trip_driver_arriving", "Driver Arriving", "Email"),
        };

        foreach (var (key, name, channel) in templates)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationTemplates')
                AND NOT EXISTS (
                    SELECT 1 FROM NotificationTemplates
                    WHERE TemplateKey = @Key AND Channel = @Channel AND IsDeleted = 0)
                INSERT INTO NotificationTemplates
                    (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
                VALUES (@Key, @Name, N'{{title}}', N'{{message}}', @Channel, 1, 'en', N'["title","message"]', GETUTCDATE(), 0);
                """,
                new { Key = key, Name = name, Channel = channel },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiDecisionRules')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM AiDecisionRules WHERE EventType = N'trip_delayed' AND TenantId IS NULL)
                    INSERT INTO AiDecisionRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                    VALUES (NULL, N'trip_delayed', 5, 3, N'["InApp","Browser","Email"]', 1);
                IF NOT EXISTS (SELECT 1 FROM AiDecisionRules WHERE EventType = N'trip_cancelled' AND TenantId IS NULL)
                    INSERT INTO AiDecisionRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                    VALUES (NULL, N'trip_cancelled', 0, 3, N'["InApp","Browser","Email"]', 1);
                IF NOT EXISTS (SELECT 1 FROM AiDecisionRules WHERE EventType = N'trip_updated' AND TenantId IS NULL)
                    INSERT INTO AiDecisionRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                    VALUES (NULL, N'trip_updated', 10, 2, N'["InApp","Browser","Email"]', 1);
                IF NOT EXISTS (SELECT 1 FROM AiDecisionRules WHERE EventType = N'trip_driver_arriving' AND TenantId IS NULL)
                    INSERT INTO AiDecisionRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                    VALUES (NULL, N'trip_driver_arriving', 5, 2, N'["InApp","Browser","Email"]', 1);
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("TripsPhase4Migration applied successfully.");
    }
}
