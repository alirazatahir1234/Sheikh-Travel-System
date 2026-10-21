using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Idempotent follow-up: activate EV26R diagnostic panel commands (STATUS/CHECK/ICCID/IMSI/APN/GPRSSET/Network)
/// even if <see cref="Gt06Ev26rCommandTemplatesMigration"/> already ran with ops-only templates.
/// </summary>
public static class Gt06Ev26rDiagnosticCommandsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        foreach (var (key, name, sort) in new[]
                 {
                     ("check", "Device Check", 75),
                     ("gprsset", "GPRSSET", 125),
                 })
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM GpsCommandDefinitions WHERE CommandKey = @Key)
                    INSERT INTO GpsCommandDefinitions
                        (CommandKey, DisplayName, Category, DangerLevel, RequiresApproval, RequiresReason, SortOrder, IsActive)
                    VALUES (@Key, @Name, N'Diagnostics', N'Low', 0, 0, @Sort, 1);
                ELSE
                    UPDATE GpsCommandDefinitions
                    SET DisplayName = @Name, Category = N'Diagnostics', IsActive = 1, SortOrder = @Sort
                    WHERE CommandKey = @Key;
                """, new { Key = key, Name = name, Sort = sort }, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE GpsCommandDefinitions SET DisplayName = N'Query APN', Category = N'Diagnostics',
                RequiresApproval = 0, RequiresReason = 0, IsActive = 1 WHERE CommandKey = N'apn';
            UPDATE GpsCommandDefinitions SET DisplayName = N'Network Settings', Category = N'Diagnostics',
                RequiresApproval = 0, RequiresReason = 0, IsActive = 1 WHERE CommandKey = N'server';

            -- Query forms (APN# / GPRSSET#) do not need set parameters.
            UPDATE GpsCommandParameterDefinitions SET IsRequired = 0
            WHERE CommandKey IN (N'apn', N'server');
            """, cancellationToken: cancellationToken));

        var diagnostics = new (string Cmd, string Payload)[]
        {
            ("status", "STATUS#"),
            ("check", "CHECK#"),
            ("iccid", "ICCID#"),
            ("imsi", "IMSI#"),
            ("apn", "APN#"),
            ("gprsset", "GPRSSET#"),
            ("server", "GPRSSET#"),
        };

        foreach (var t in diagnostics)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE t
                SET Transport = N'Auto', PayloadTemplate = @Payload, TraccarType = N'custom',
                    IsActive = 1, UpdatedAt = SYSUTCDATETIME()
                FROM GpsCommandTemplates t
                INNER JOIN TrackerModels m ON m.Id = t.TrackerModelId
                WHERE m.CatalogKey IN @Keys AND t.CommandKey = @Cmd AND t.TemplateVersion = 1;

                INSERT INTO GpsCommandTemplates
                    (TrackerModelId, CommandKey, Transport, PayloadTemplate, TraccarType, TemplateVersion, IsActive)
                SELECT m.Id, @Cmd, N'Auto', @Payload, N'custom', 1, 1
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
                    Keys = GpsGt06CommandMap.Gt06CatalogKeys.ToArray()
                },
                cancellationToken: cancellationToken));
        }

        // EV26R gets the full panel; VG03 panel is narrowed in Gt06ModelSpecificCommandPanelMigration.
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

        logger.LogInformation("Gt06Ev26rDiagnosticCommandsMigration applied successfully.");
    }
}
