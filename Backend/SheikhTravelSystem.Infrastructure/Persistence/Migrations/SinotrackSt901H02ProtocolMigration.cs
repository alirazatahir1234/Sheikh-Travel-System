using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Sinotrack ST-901 is H02 / TCP 5013 (Traccar official device list), not GT06 / 5023.
/// Wrong catalog port caused devices to be pointed at GT06 — live heartbeats work, but
/// H02 store-and-forward (~2000 points) never uploads. Correct model + linked devices.
/// </summary>
public static class SinotrackSt901H02ProtocolMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;",
            cancellationToken: cancellationToken));

        var models = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TrackerModels
            SET Protocol = N'h02',
                ProtocolLabel = N'H02',
                DefaultPort = 5013,
                UpdatedAt = SYSUTCDATETIME()
            WHERE CatalogKey = N'sinotrack_st901'
              AND (Protocol <> N'h02' OR DefaultPort <> 5013 OR ProtocolLabel <> N'H02');
            """, cancellationToken: cancellationToken));

        var devices = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE d
            SET Protocol = N'h02',
                UpdatedAt = SYSUTCDATETIME()
            FROM GpsDevices d
            LEFT JOIN TrackerModels m ON m.Id = d.TrackerModelId
            WHERE d.IsDeleted = 0
              AND (
                    d.TrackerModelKey = N'sinotrack_st901'
                 OR m.CatalogKey = N'sinotrack_st901'
                 OR d.Model LIKE N'%ST-901%'
                 OR d.Model LIKE N'%ST901%'
              )
              AND (d.Protocol IS NULL OR d.Protocol <> N'h02');
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "SinotrackSt901H02ProtocolMigration: TrackerModels updated={Models}, GpsDevices updated={Devices}.",
            models, devices);
    }
}
