using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// ACC / ignition ON-OFF SMS alerts via SheikhGo notification pipeline (not Traccar SMS).
/// Seeds cooldown rules + SMS/InApp templates; documents VG03 ACC wiring prerequisite.
/// </summary>
public static class IgnitionSmsAlertsMigration
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
            await DocumentVg03AccPrerequisiteAsync(connection, cancellationToken);

            logger.LogInformation("IgnitionSmsAlertsMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IgnitionSmsAlertsMigration failed.");
            throw;
        }
    }

    private static async Task SeedCooldownRulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var rules = new (string EventType, int Cooldown, int MinPriority, string Channels)[]
        {
            ("ignition_on", 5, 3, "[\"InApp\",\"Browser\",\"Push\",\"Sms\"]"),
            ("ignition_off", 5, 3, "[\"InApp\",\"Browser\",\"Push\",\"Sms\"]"),
        };

        foreach (var r in rules)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiCooldownRules')
                AND NOT EXISTS (SELECT 1 FROM AiCooldownRules WHERE EventType = @EventType AND TenantId IS NULL)
                INSERT INTO AiCooldownRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
                VALUES (NULL, @EventType, @Cooldown, @MinPriority, @Channels, 1)
                """,
                new { r.EventType, r.Cooldown, r.MinPriority, r.Channels },
                cancellationToken: ct));
        }
    }

    private static async Task SeedTemplatesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, "NotificationTemplates", ct))
            return;

        var templates = new (string Key, string Name, string Subject, string Body, string Channel)[]
        {
            ("ignition_on_alert", "Ignition ON", "Ignition ON: {{title}}", "{{message}}", "InApp"),
            ("ignition_on_alert", "Ignition ON", "Ignition ON: {{title}}", "{{message}}", "Sms"),
            ("ignition_off_alert", "Ignition OFF", "Ignition OFF: {{title}}", "{{message}}", "InApp"),
            ("ignition_off_alert", "Ignition OFF", "Ignition OFF: {{title}}", "{{message}}", "Sms"),
        };

        // TenantId is NOT NULL; UQ_NotificationTemplates_Key_Channel is global (Key+Channel).
        // Seed once against the lowest tenant id (platform default).
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

    /// <summary>
    /// VG03 (IMEI 862292058090598): SheikhGo auto-SMS is ready once ACC reports ignition;
    /// physical ACC wire remains the prerequisite. Traccar web/mail notifications stay unchanged.
    /// </summary>
    private static async Task DocumentVg03AccPrerequisiteAsync(
        System.Data.IDbConnection connection,
        CancellationToken ct)
    {
        if (!await ColumnExistsAsync(connection, "GpsDevices", "InstallationNotes", ct))
            return;

        const string note =
            "ACC ignition SMS: SheikhGo ready (ignition_on/off → SMS). Wire ACC sensor first — " +
            "events fire only when Traccar reports attributes.ignition. Traccar web/mail unchanged.";

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE GpsDevices
            SET InstallationNotes = CASE
                    WHEN InstallationNotes IS NULL OR LTRIM(RTRIM(InstallationNotes)) = N'' THEN @Note
                    WHEN InstallationNotes LIKE N'%ACC ignition SMS%' THEN InstallationNotes
                    ELSE LEFT(InstallationNotes + N' | ' + @Note, 500)
                END,
                UpdatedAt = GETUTCDATE()
            WHERE IsDeleted = 0
              AND (
                    UniqueId = N'862292058090598'
                    OR UniqueId LIKE N'%862292058090598%'
                    OR Name LIKE N'%VG03%'
                    OR Model LIKE N'%VG03%'
                  )
            """,
            new { Note = note },
            cancellationToken: ct));
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.IDbConnection connection, string table, CancellationToken ct) =>
        await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table) THEN 1 ELSE 0 END
            """, new { Table = table }, cancellationToken: ct));

    private static async Task<bool> ColumnExistsAsync(
        System.Data.IDbConnection connection, string table, string column, CancellationToken ct) =>
        await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN COL_LENGTH(@Table, @Column) IS NOT NULL THEN 1 ELSE 0 END
            """, new { Table = table, Column = column }, cancellationToken: ct));
}
