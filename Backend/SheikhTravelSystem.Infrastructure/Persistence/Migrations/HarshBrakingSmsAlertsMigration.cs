using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Harsh braking SMS via SheikhGo notification pipeline (not device SIM collision text).
/// Maps VG03 G-sensor <c>accident</c> false positives to <c>harsh_braking</c> at ingest/sync.
/// </summary>
public static class HarshBrakingSmsAlertsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await SeedCooldownRulesAsync(connection, cancellationToken);
            await SeedTemplatesAsync(connection, cancellationToken);

            logger.LogInformation("HarshBrakingSmsAlertsMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HarshBrakingSmsAlertsMigration failed.");
            throw;
        }
    }

    private static async Task SeedCooldownRulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        const string eventType = "harsh_braking";
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiCooldownRules')
            AND NOT EXISTS (SELECT 1 FROM AiCooldownRules WHERE EventType = @EventType AND TenantId IS NULL)
            INSERT INTO AiCooldownRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
            VALUES (NULL, @EventType, 10, 3, N'["InApp","Browser","Push","Sms"]', 1)
            """,
            new { EventType = eventType },
            cancellationToken: ct));
    }

    private static async Task SeedTemplatesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, "NotificationTemplates", ct))
            return;

        var templates = new (string Key, string Name, string Subject, string Body, string Channel)[]
        {
            ("harsh_braking_alert", "Harsh braking", "Harsh braking: {{title}}", "{{message}}", "InApp"),
            ("harsh_braking_alert", "Harsh braking", "Harsh braking: {{title}}", "{{message}}", "Sms"),
        };

        foreach (var t in templates)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (
                    SELECT 1 FROM NotificationTemplates
                    WHERE TemplateKey = @Key AND Channel = @Channel AND IsDeleted = 0)
                INSERT INTO NotificationTemplates
                    (TenantId, TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
                SELECT TOP 1 ten.Id, @Key, @Name, @Subject, @Body, @Channel, 1, N'en',
                       N'["title","message"]', GETUTCDATE(), 0
                FROM Tenants ten
                ORDER BY ten.Id;
                """,
                new { t.Key, t.Name, t.Subject, t.Body, t.Channel },
                cancellationToken: ct));
        }
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.IDbConnection connection, string table, CancellationToken ct) =>
        await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table) THEN 1 ELSE 0 END
            """, new { Table = table }, cancellationToken: ct));
}
