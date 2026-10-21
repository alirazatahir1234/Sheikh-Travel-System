using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Model-specific Commands-panel templates:
/// VG03 (jimi_vg03) → RESET, STATUS, GPRSSET/Network, RELAY stop/resume, URL# position only.
/// EV26R (jimi_ev26r) → full ops + diagnostics (CHECK/ICCID/IMSI/APN/WHERE#).
/// </summary>
public static class Gt06ModelSpecificCommandPanelMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // VG03 position = URL# (documented); EV26R / other GT06 = WHERE#.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE t
            SET PayloadTemplate = N'URL#', Transport = N'Auto', TraccarType = N'custom',
                IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_vg03' AND t.CommandKey = N'positionSingle' AND t.TemplateVersion = 1;

            UPDATE t
            SET PayloadTemplate = N'WHERE#', Transport = N'Auto', TraccarType = N'custom',
                IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_ev26r' AND t.CommandKey = N'positionSingle' AND t.TemplateVersion = 1;
            """, cancellationToken: cancellationToken));

        // Ensure VG03 panel templates exist.
        var vg03Panel = new (string Cmd, string Payload, string? Traccar)[]
        {
            ("restart", "RESET#", "custom"),
            ("positionSingle", "URL#", "custom"),
            ("engineStop", "RELAY,1#", "engineStop"),
            ("engineResume", "RELAY,0#", "engineResume"),
            ("status", "STATUS#", "custom"),
            ("gprsset", "GPRSSET#", "custom"),
            ("server", "GPRSSET#", "custom"),
        };

        foreach (var t in vg03Panel)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE t
                SET Transport = N'Auto', PayloadTemplate = @Payload, TraccarType = @Traccar,
                    IsActive = 1, UpdatedAt = SYSUTCDATETIME()
                FROM GpsCommandTemplates t
                INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
                WHERE m.CatalogKey = N'jimi_vg03' AND t.CommandKey = @Cmd AND t.TemplateVersion = 1;

                INSERT INTO GpsCommandTemplates
                    (TrackerModelId, CommandKey, Transport, PayloadTemplate, TraccarType, TemplateVersion, IsActive)
                SELECT m.Id, @Cmd, N'Auto', @Payload, @Traccar, 1, 1
                FROM TrackerModels m
                WHERE m.CatalogKey = N'jimi_vg03'
                  AND NOT EXISTS (
                      SELECT 1 FROM GpsCommandTemplates x
                      WHERE x.TrackerModelId = m.Id AND x.CommandKey = @Cmd AND x.TemplateVersion = 1);
                """,
                new { t.Cmd, Payload = t.Payload, Traccar = t.Traccar },
                cancellationToken: cancellationToken));
        }

        // VG03: activate only documented panel; hide CHECK/ICCID/IMSI/APN/PARAM/etc.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE t SET IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_vg03' AND t.CommandKey IN @Vg03Keys;

            UPDATE t SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_vg03' AND t.CommandKey NOT IN @Vg03Keys;
            """,
            new { Vg03Keys = GpsGt06CommandMap.Vg03PanelCommandKeys },
            cancellationToken: cancellationToken));

        // EV26R: full panel active.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE t SET IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_ev26r' AND t.CommandKey IN @Ev26Keys;

            UPDATE t SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
            FROM GpsCommandTemplates t
            INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
            WHERE m.CatalogKey = N'jimi_ev26r' AND t.CommandKey NOT IN @Ev26Keys;
            """,
            new { Ev26Keys = GpsGt06CommandMap.Ev26rPanelCommandKeys },
            cancellationToken: cancellationToken));

        logger.LogInformation(
            "Gt06ModelSpecificCommandPanelMigration applied (VG03={Vg03Count} keys, EV26R={Ev26Count} keys).",
            GpsGt06CommandMap.Vg03PanelCommandKeys.Length,
            GpsGt06CommandMap.Ev26rPanelCommandKeys.Length);
    }
}
