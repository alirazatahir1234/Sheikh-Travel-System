using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Per-vehicle per-zone inside/outside state for geofence crossing detection.
/// Seeds IsInside from the last historical enter/exit alert so existing vehicles keep continuity.
/// </summary>
public static class VehicleGeofenceStateMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'VehicleGeofenceState', N'U') IS NULL
            BEGIN
                CREATE TABLE VehicleGeofenceState (
                    VehicleId INT NOT NULL,
                    GeofenceId INT NOT NULL,
                    TenantId INT NOT NULL,
                    IsInside BIT NOT NULL,
                    SinceUtc DATETIME2 NOT NULL,
                    PendingInside BIT NULL,
                    PendingCount INT NOT NULL CONSTRAINT DF_VehicleGeofenceState_PendingCount DEFAULT 0,
                    PendingSinceUtc DATETIME2 NULL,
                    LastEvaluatedUtc DATETIME2 NOT NULL,
                    LastLatitude FLOAT NULL,
                    LastLongitude FLOAT NULL,
                    CONSTRAINT PK_VehicleGeofenceState PRIMARY KEY (VehicleId, GeofenceId)
                );
                CREATE INDEX IX_VehicleGeofenceState_Tenant_Geofence
                    ON VehicleGeofenceState(TenantId, GeofenceId) INCLUDE (IsInside, SinceUtc, LastEvaluatedUtc);
            END
            """, cancellationToken: cancellationToken));

        var seeded = await connection.ExecuteAsync(new CommandDefinition("""
            ;WITH last AS (
                SELECT e.VehicleId, e.GeofenceId, e.EventType, e.Timestamp,
                       ROW_NUMBER() OVER (PARTITION BY e.VehicleId, e.GeofenceId ORDER BY e.Timestamp DESC) AS rn
                FROM GpsAlertEvents e
                WHERE e.GeofenceId IS NOT NULL AND e.IsDeleted = 0
                  AND e.EventType IN (N'geofence_enter', N'geofence_exit')
            )
            INSERT INTO VehicleGeofenceState (VehicleId, GeofenceId, TenantId, IsInside, SinceUtc, LastEvaluatedUtc)
            SELECT l.VehicleId, l.GeofenceId, g.TenantId,
                   CASE WHEN l.EventType = N'geofence_enter' THEN 1 ELSE 0 END,
                   l.Timestamp, l.Timestamp
            FROM last l
            INNER JOIN Geofences g ON g.Id = l.GeofenceId AND g.IsDeleted = 0 AND g.TenantId IS NOT NULL
            INNER JOIN Vehicles v ON v.Id = l.VehicleId AND v.TenantId = g.TenantId
            WHERE l.rn = 1
              AND NOT EXISTS (SELECT 1 FROM VehicleGeofenceState s
                              WHERE s.VehicleId = l.VehicleId AND s.GeofenceId = l.GeofenceId);
            """, cancellationToken: cancellationToken));

        logger.LogInformation("VehicleGeofenceStateMigration applied (seeded {Rows} state rows from alert history).", seeded);
    }
}
