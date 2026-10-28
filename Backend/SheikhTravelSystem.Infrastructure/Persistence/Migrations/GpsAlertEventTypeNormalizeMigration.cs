using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Canonicalize historical Traccar misspellings (ignitionon, deviceoffline, …)
/// so Alerts filter chips and module gates match AlertTypeCatalog.
/// Does not invent new alert rows — only renames EventType on existing GpsAlertEvents.
/// </summary>
public static class GpsAlertEventTypeNormalizeMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();

        var map = new (string From, string To)[]
        {
            ("ignitionon", "ignition_on"),
            ("ignitionoff", "ignition_off"),
            ("geofenceenter", "geofence_enter"),
            ("geofenceexit", "geofence_exit"),
            ("deviceonline", "online"),
            ("device_online", "online"),
            ("deviceoffline", "vehicle_offline"),
            ("device_offline", "vehicle_offline"),
            ("gps_offline", "vehicle_offline"),
            ("offline", "vehicle_offline"),
        };

        var total = 0;
        foreach (var (from, to) in map)
        {
            var n = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE GpsAlertEvents
                SET EventType = @To
                WHERE IsDeleted = 0 AND LOWER(EventType) = @From AND EventType <> @To
                """,
                new { From = from, To = to },
                cancellationToken: ct));
            total += n;
        }

        if (total > 0)
            logger.LogInformation("GpsAlertEventTypeNormalizeMigration updated {Count} EventType row(s).", total);
        else
            logger.LogInformation("GpsAlertEventTypeNormalizeMigration: no EventType rows needed update.");
    }
}
