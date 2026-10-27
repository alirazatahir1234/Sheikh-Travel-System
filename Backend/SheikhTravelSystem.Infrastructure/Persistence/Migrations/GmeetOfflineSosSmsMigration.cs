using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Gmeet offline / no-GPRS SMS to device SOS number (separate from ACC ignition SMS).
/// Adds SosNumber column, seeds Gmeet catalog entry, SMS template + cooldown rule.
/// </summary>
public static class GmeetOfflineSosSmsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await EnsureSosNumberColumnAsync(connection, cancellationToken);
            await SeedGmeetCatalogAsync(connection, cancellationToken);
            await SeedCooldownRulesAsync(connection, cancellationToken);
            await SeedTemplatesAsync(connection, cancellationToken);

            logger.LogInformation("GmeetOfflineSosSmsMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GmeetOfflineSosSmsMigration failed.");
            throw;
        }
    }

    private static async Task EnsureSosNumberColumnAsync(
        System.Data.IDbConnection connection,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('GpsDevices', 'SosNumber') IS NULL
                ALTER TABLE GpsDevices ADD SosNumber NVARCHAR(30) NULL;
            """, cancellationToken: ct));
    }

    private static async Task SeedGmeetCatalogAsync(
        System.Data.IDbConnection connection,
        CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, "TrackerBrands", ct) ||
            !await TableExistsAsync(connection, "TrackerModels", ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM TrackerBrands WHERE Name = @Name)
                INSERT INTO TrackerBrands (Name, IsActive) VALUES (@Name, 1);
            """,
            new { Name = GmeetDeviceMatcher.BrandName },
            cancellationToken: ct));

        var brandId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT Id FROM TrackerBrands WHERE Name = @Name",
            new { Name = GmeetDeviceMatcher.BrandName },
            cancellationToken: ct));

        // Protocol default gt06 — many white-label Chinese units; Ali can retarget via TrackerModels if needed.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM TrackerModels WHERE CatalogKey = @CatalogKey)
                INSERT INTO TrackerModels (
                    TrackerBrandId, Name, CatalogKey, Protocol, ProtocolLabel, DefaultPort,
                    SupportsEngineCutOff, SupportsRelay, IsActive)
                VALUES (
                    @BrandId, N'Gmeet Generic', @CatalogKey, N'gt06', N'GT06', 5023,
                    1, 1, 1);
            ELSE
                UPDATE TrackerModels SET
                    TrackerBrandId = @BrandId,
                    Name = N'Gmeet Generic',
                    Protocol = N'gt06',
                    ProtocolLabel = N'GT06',
                    DefaultPort = 5023,
                    SupportsEngineCutOff = 1,
                    SupportsRelay = 1,
                    IsActive = 1
                WHERE CatalogKey = @CatalogKey;
            """,
            new { BrandId = brandId, CatalogKey = GmeetDeviceMatcher.DefaultCatalogKey },
            cancellationToken: ct));
    }

    private static async Task SeedCooldownRulesAsync(
        System.Data.IDbConnection connection,
        CancellationToken ct)
    {
        // Align with once-until-online GpsAlertEvents gate; Redis cooldown is a secondary spam guard.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AiCooldownRules')
            AND NOT EXISTS (SELECT 1 FROM AiCooldownRules WHERE EventType = N'gmeet_device_offline' AND TenantId IS NULL)
            INSERT INTO AiCooldownRules (TenantId, EventType, CooldownMinutes, MinPriority, ChannelsJson, IsActive)
            VALUES (NULL, N'gmeet_device_offline', 30, 3, N'["Sms"]', 1)
            """, cancellationToken: ct));
    }

    private static async Task SeedTemplatesAsync(
        System.Data.IDbConnection connection,
        CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, "NotificationTemplates", ct))
            return;

        // TenantId is NOT NULL; UQ_NotificationTemplates_Key_Channel is global (Key+Channel).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM NotificationTemplates
                WHERE TemplateKey = N'gmeet_offline_sos' AND Channel = N'Sms' AND IsDeleted = 0)
            INSERT INTO NotificationTemplates
                (TenantId, TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
            SELECT TOP 1 ten.Id,
                N'gmeet_offline_sos',
                N'Gmeet Offline SOS',
                N'{{title}}',
                N'{{message}}',
                N'Sms',
                1,
                N'en',
                N'["title","message"]',
                GETUTCDATE(),
                0
            FROM Tenants ten
            ORDER BY ten.Id;
            """, cancellationToken: ct));
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.IDbConnection connection, string table, CancellationToken ct) =>
        await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table) THEN 1 ELSE 0 END
            """, new { Table = table }, cancellationToken: ct));
}
