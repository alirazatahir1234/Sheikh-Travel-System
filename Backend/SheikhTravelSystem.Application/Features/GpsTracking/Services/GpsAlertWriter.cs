using Dapper;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Single insert path for GpsAlertEvents — every detector (ingest-time, Traccar event sync,
/// offline hosted job) routes through here so severity, status, and driver attribution stay
/// consistent instead of being duplicated per call site.
/// </summary>
public static class GpsAlertWriter
{
    public static async Task<int> InsertAsync(
        System.Data.IDbConnection connection,
        int vehicleId,
        double latitude,
        double longitude,
        decimal speed,
        string eventType,
        string message,
        DateTime timestamp,
        int? ruleId = null,
        int? geofenceId = null,
        int? driverId = null,
        string? externalEventId = null,
        CancellationToken cancellationToken = default)
    {
        driverId ??= await ResolveDriverIdAsync(connection, vehicleId, cancellationToken);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO GpsAlertEvents
              (RuleId, VehicleId, GeofenceId, DriverId, EventType, Severity, Status,
               Latitude, Longitude, Speed, Message, Timestamp, ExternalEventId, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
              (@RuleId, @VehicleId, @GeofenceId, @DriverId, @EventType, @Severity, 'active',
               @Latitude, @Longitude, @Speed, @Message, @Timestamp, @ExternalEventId, GETUTCDATE(), 0)
            """,
            new
            {
                RuleId = ruleId,
                VehicleId = vehicleId,
                GeofenceId = geofenceId,
                DriverId = driverId,
                EventType = eventType,
                Severity = SeverityFor(eventType),
                Latitude = latitude,
                Longitude = longitude,
                Speed = speed,
                Message = message,
                Timestamp = timestamp,
                ExternalEventId = externalEventId
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>Kept in sync with the backfill CASE in GpsAlertsPhase8Migration.</summary>
    public static string SeverityFor(string eventType) => eventType switch
    {
        "sos" or "power_cut" => "critical",
        "speed_exceeded" or "geofence_exit" or "vehicle_offline" or "device_offline" or "alarm" => "high",
        "ignition_on" or "low_battery" or "gps_lost" or "low_fuel" or "geofence_enter" => "medium",
        "ignition_off" or "online" or "device_online" or "vehicle_online" => "low",
        _ => "medium"
    };

    private static async Task<int?> ResolveDriverIdAsync(
        System.Data.IDbConnection connection, int vehicleId, CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT COALESCE(
                (SELECT DriverId FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId),
                (SELECT TOP 1 a.DriverId
                 FROM AssignmentHistory a
                 WHERE a.VehicleId = @VehicleId AND a.IsDeleted = 0
                   AND a.Status IN (N'Active', N'Scheduled') AND a.DriverId IS NOT NULL
                 ORDER BY CASE WHEN a.Status = N'Active' THEN 0 ELSE 1 END, a.StartAt DESC)
            )
            """,
            new { VehicleId = vehicleId },
            cancellationToken: cancellationToken));
    }
}
