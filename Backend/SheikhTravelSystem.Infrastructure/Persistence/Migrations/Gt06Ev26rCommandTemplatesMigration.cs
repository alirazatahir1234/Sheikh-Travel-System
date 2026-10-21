using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Fixes GT06 / Jimi EV26R / VG03 command templates and enables engine cut-off on VG03 devices.
/// Restart → RESET# via custom (never rebootDevice). Position → WHERE# via custom.
/// Engine stop/resume → RELAY payloads with Traccar engineStop/engineResume.
/// Diagnostics → STATUS# / CHECK# / ICCID# / IMSI# / APN# / GPRSSET# via custom.
/// </summary>
public static class Gt06Ev26rCommandTemplatesMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            DECLARE @JimiId INT = (SELECT TOP 1 Id FROM TrackerBrands WHERE BrandKey = N'jimi' OR Name LIKE N'Jimi%');
            IF @JimiId IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM TrackerModels WHERE CatalogKey = N'jimi_ev26r')
                INSERT INTO TrackerModels (
                    TrackerBrandId, Name, CatalogKey, Protocol, ProtocolLabel, DefaultPort,
                    SupportsEngineCutOff, SupportsRelay, SupportsIgnition, SupportsBatteryMonitoring,
                    FirmwareHint, Description, IsActive)
                VALUES (
                    @JimiId, N'EV26R', N'jimi_ev26r', N'gt06', N'GT06', 5023,
                    1, 1, 1, 1, N'1.0+', N'Jimi EV26R GPS tracker', 1);

            UPDATE TrackerModels
            SET SupportsEngineCutOff = 1, SupportsRelay = 1
            WHERE CatalogKey IN (N'jimi_vg03', N'jimi_ev26r', N'jimi_gt06', N'concox_gt06n', N'sinotrack_st901', N'concox_at6');
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
            SELECT m.Id, N'EngineCut', 1
            FROM TrackerModels m
            WHERE m.CatalogKey IN (N'jimi_vg03', N'jimi_ev26r', N'jimi_gt06', N'concox_gt06n', N'sinotrack_st901', N'concox_at6')
              AND NOT EXISTS (
                  SELECT 1 FROM TrackerModelCapabilities c
                  WHERE c.TrackerModelId = m.Id AND c.CapabilityKey = N'EngineCut');

            INSERT INTO TrackerModelCapabilities (TrackerModelId, CapabilityKey, IsEnabled)
            SELECT m.Id, N'Relay', 1
            FROM TrackerModels m
            WHERE m.CatalogKey IN (N'jimi_vg03', N'jimi_ev26r', N'jimi_gt06', N'concox_gt06n', N'sinotrack_st901', N'concox_at6')
              AND NOT EXISTS (
                  SELECT 1 FROM TrackerModelCapabilities c
                  WHERE c.TrackerModelId = m.Id AND c.CapabilityKey = N'Relay');

            UPDATE TrackerModelCapabilities
            SET IsEnabled = 1
            WHERE CapabilityKey IN (N'EngineCut', N'Relay')
              AND TrackerModelId IN (
                  SELECT Id FROM TrackerModels
                  WHERE CatalogKey IN (N'jimi_vg03', N'jimi_ev26r', N'jimi_gt06', N'concox_gt06n', N'sinotrack_st901', N'concox_at6'));
            """, cancellationToken: cancellationToken));

        // Ensure command definitions exist (check / gprsset are EV26R diagnostics).
        var definitions = new (string Key, string Name, string Cat, string? Cap, string Danger, bool Approval, bool Reason, int Sort)[]
        {
            ("check", "Device Check", "Diagnostics", null, "Low", false, false, 75),
            ("gprsset", "GPRSSET", "Diagnostics", null, "Low", false, false, 125),
        };
        foreach (var c in definitions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM GpsCommandDefinitions WHERE CommandKey = @Key)
                    INSERT INTO GpsCommandDefinitions
                        (CommandKey, DisplayName, Category, RequiredCapabilityKey, DangerLevel, RequiresApproval, RequiresReason, SortOrder)
                    VALUES (@Key, @Name, @Cat, @Cap, @Danger, @Approval, @Reason, @Sort);
                ELSE
                    UPDATE GpsCommandDefinitions
                    SET DisplayName = @Name, Category = @Cat, IsActive = 1, SortOrder = @Sort
                    WHERE CommandKey = @Key;
                """, new { c.Key, c.Name, c.Cat, c.Cap, c.Danger, c.Approval, c.Reason, c.Sort },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE GpsCommandDefinitions
            SET DisplayName = N'Query APN', Category = N'Diagnostics', RequiresApproval = 0, RequiresReason = 0, IsActive = 1
            WHERE CommandKey = N'apn';

            UPDATE GpsCommandDefinitions
            SET DisplayName = N'Network Settings', Category = N'Diagnostics', RequiresApproval = 0, RequiresReason = 0, IsActive = 1
            WHERE CommandKey = N'server';

            UPDATE GpsCommandDefinitions
            SET DisplayName = N'Device Status', Category = N'Diagnostics', IsActive = 1
            WHERE CommandKey = N'status';
            """, cancellationToken: cancellationToken));

        var panel = new (string Cmd, string Payload, string? Traccar)[]
        {
            ("restart", "RESET#", "custom"),
            ("positionSingle", "WHERE#", "custom"),
            ("engineStop", "RELAY,1#", "engineStop"),
            ("engineResume", "RELAY,0#", "engineResume"),
            ("status", "STATUS#", "custom"),
            ("check", "CHECK#", "custom"),
            ("iccid", "ICCID#", "custom"),
            ("imsi", "IMSI#", "custom"),
            ("apn", "APN#", "custom"),
            ("gprsset", "GPRSSET#", "custom"),
            // SERVER is set-only; GPRSSET# returns Server/APN snapshot (manual §4.24).
            ("server", "GPRSSET#", "custom"),
        };

        foreach (var t in panel)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE t
                SET Transport = N'Auto',
                    PayloadTemplate = @Payload,
                    TraccarType = @Traccar,
                    IsActive = 1,
                    UpdatedAt = SYSUTCDATETIME()
                FROM GpsCommandTemplates t
                INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
                WHERE m.CatalogKey IN @Keys
                  AND t.CommandKey = @Cmd
                  AND t.TemplateVersion = 1;

                INSERT INTO GpsCommandTemplates
                    (TrackerModelId, CommandKey, Transport, PayloadTemplate, TraccarType, TemplateVersion, IsActive)
                SELECT m.Id, @Cmd, N'Auto', @Payload, @Traccar, 1, 1
                FROM TrackerModels m
                WHERE m.CatalogKey IN @Keys
                  AND NOT EXISTS (
                      SELECT 1 FROM GpsCommandTemplates x
                      WHERE x.TrackerModelId = m.Id AND x.CommandKey = @Cmd AND x.TemplateVersion = 1);
                """,
                new
                {
                    t.Cmd,
                    Payload = t.Payload,
                    Traccar = t.Traccar,
                    Keys = GpsGt06CommandMap.Gt06CatalogKeys.ToArray()
                },
                cancellationToken: cancellationToken));
        }

        // EV26R: full panel. VG03 narrowed later by Gt06ModelSpecificCommandPanelMigration.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE t SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_ev26r' AND t.CommandKey NOT IN @Ev26Keys;

            UPDATE t SET IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_ev26r' AND t.CommandKey IN @Ev26Keys;
            """,
            new { Ev26Keys = GpsGt06CommandMap.Ev26rPanelCommandKeys },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            DECLARE @Vg03ModelId INT = (SELECT TOP 1 Id FROM TrackerModels WHERE CatalogKey = N'jimi_vg03');
            DECLARE @Ev26ModelId INT = (SELECT TOP 1 Id FROM TrackerModels WHERE CatalogKey = N'jimi_ev26r');

            UPDATE d
            SET SupportsEngineCutoff = 1,
                SupportsRelay = 1,
                RelayPurpose = COALESCE(NULLIF(d.RelayPurpose, N''), N'EngineImmobilizer'),
                RelayOutput = COALESCE(NULLIF(d.RelayOutput, N''), N'output1'),
                TrackerModelId = COALESCE(d.TrackerModelId, @Vg03ModelId, @Ev26ModelId),
                TrackerModelKey = COALESCE(NULLIF(d.TrackerModelKey, N''), N'jimi_vg03'),
                UpdatedAt = SYSUTCDATETIME()
            FROM GpsDevices d
            WHERE d.IsDeleted = 0
              AND (
                  d.Id = 1
                  OR d.Name LIKE N'%VG03%'
                  OR d.Name LIKE N'%Jimi%VG03%'
                  OR d.Model LIKE N'%VG03%'
                  OR d.Model LIKE N'%EV26%'
                  OR d.TrackerModelKey IN (N'jimi_vg03', N'jimi_ev26r', N'jimi_gt06')
                  OR d.TrackerModelId IN (SELECT Id FROM TrackerModels WHERE CatalogKey IN (N'jimi_vg03', N'jimi_ev26r', N'jimi_gt06'))
              );
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Gt06Ev26rCommandTemplatesMigration applied successfully.");
    }
}
