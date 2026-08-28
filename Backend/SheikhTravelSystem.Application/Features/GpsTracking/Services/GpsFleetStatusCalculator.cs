using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using Microsoft.Extensions.Options;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Single source of truth for local-data fleet-status counts — shared by GetGpsFleetStatusLocalQuery
/// (on-demand, for the live KPI strip) and GpsFleetStatusSnapshotHostedService (periodic, for
/// history/trend charts) so the two can never drift apart. Approximates the frontend's
/// resolveFleetStatus() rule (core/utils/gps-status.util.ts) in SQL for aggregate counting — good
/// enough for dashboard-level totals, not intended to reproduce every per-vehicle edge case.
/// </summary>
public static class GpsFleetStatusCalculator
{
    // Mutually-exclusive dashboard buckets:
    // NeverSeen: no current location row (with tracker), Offline: stale row,
    // Moving: fresh row + speed >= threshold, Idle: fresh row + speed below threshold.
    private const string StatusBucketSql = """
        CASE
          WHEN vcl.VehicleId IS NULL THEN
            CASE WHEN v.GpsDeviceId IS NOT NULL THEN 'never_seen' ELSE 'offline' END
          WHEN DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) > @OfflineStaleMinutes THEN 'offline'
          WHEN ISNULL(vcl.Speed, 0) >= @MovingThresholdKmh THEN 'moving'
          ELSE 'idle'
        END
        """;

    public static async Task<GpsFleetStatusLocalDto> ComputeAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        IOptions<GpsSettings> gpsSettings,
        IOptions<TraccarOptions> traccarOptions,
        CancellationToken cancellationToken = default,
        DataScopeResult? scope = null)
    {
        var staleMinutes = gpsSettings.Value.OfflineStaleMinutes <= 0
            ? 10
            : gpsSettings.Value.OfflineStaleMinutes;
        var movingThresholdKmh = traccarOptions.Value.MovingSpeedKmh > 0
            ? traccarOptions.Value.MovingSpeedKmh
            : gpsSettings.Value.FleetMovingSpeedKmh;

        var vehicleClauses = new List<string>
        {
            "v.TenantId = @TenantId",
            "v.IsDeleted = 0",
            "v.Status <> 5"
        };
        var parameters = new DynamicParameters(new
        {
            TenantId = tenantId,
            OfflineStaleMinutes = staleMinutes,
            MovingThresholdKmh = movingThresholdKmh
        });
        if (scope is not null)
            DataScopeSql.ApplyVehicleScope(parameters, scope, "v", vehicleClauses);

        var vehicleWhere = string.Join(" AND ", vehicleClauses);

        var counts = await connection.QuerySingleAsync<(int Total, int Moving, int Idle, int NeverSeen, int Offline)>(
            new CommandDefinition(
                $"""
                SELECT
                  COUNT(*) AS Total,
                  ISNULL(SUM(CASE WHEN Bucket = 'moving' THEN 1 ELSE 0 END), 0) AS Moving,
                  ISNULL(SUM(CASE WHEN Bucket = 'idle' THEN 1 ELSE 0 END), 0) AS Idle,
                  ISNULL(SUM(CASE WHEN Bucket = 'never_seen' THEN 1 ELSE 0 END), 0) AS NeverSeen,
                  ISNULL(SUM(CASE WHEN Bucket = 'offline' THEN 1 ELSE 0 END), 0) AS Offline
                FROM (
                  SELECT {StatusBucketSql} AS Bucket
                  FROM Vehicles v
                  LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                  WHERE {vehicleWhere}
                ) x
                """,
                parameters,
                cancellationToken: cancellationToken));

        var todayStart = DateTime.UtcNow.Date;
        var alertClauses = new List<string>
        {
            "v.TenantId = @TenantId",
            "e.IsDeleted = 0",
            "e.Timestamp >= @TodayStart"
        };
        var alertParams = new DynamicParameters(new { TenantId = tenantId, TodayStart = todayStart });
        if (scope is not null)
            DataScopeSql.ApplyVehicleScope(alertParams, scope, "v", alertClauses);
        var alertWhere = string.Join(" AND ", alertClauses);

        var alertsToday = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            SELECT COUNT(*)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE {alertWhere}
            """,
            alertParams,
            cancellationToken: cancellationToken));

        // "Online" = vehicles with a fresh GPS fix (moving + idle). Matches Angular live-map
        // fleetCounts.online and operator expectation: ignition-off but pinging still counts online.
        // Mutually exclusive rollup: Total = Online + Offline + NeverSeen
        // where Online = Moving + Idle (+ Parked when that bucket is populated).
        var online = counts.Moving + counts.Idle;

        return new GpsFleetStatusLocalDto(
            counts.Total,
            online,
            counts.Offline,
            counts.Moving,
            counts.Idle,
            0,
            counts.NeverSeen,
            0,
            alertsToday);
    }
}
