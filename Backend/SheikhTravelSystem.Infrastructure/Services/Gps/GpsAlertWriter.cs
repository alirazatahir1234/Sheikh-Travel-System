using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsAlertWriter : IGpsAlertWriter
{
    public async Task<int> InsertAsync(
        IDbConnection connection,
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
                Severity = GpsAlertSeverity.SeverityFor(eventType),
                Latitude = latitude,
                Longitude = longitude,
                Speed = speed,
                Message = message,
                Timestamp = timestamp,
                ExternalEventId = externalEventId
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int?> ResolveDriverIdAsync(
        IDbConnection connection, int vehicleId, CancellationToken cancellationToken)
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
