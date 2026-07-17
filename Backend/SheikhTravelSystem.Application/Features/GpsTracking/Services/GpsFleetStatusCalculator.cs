using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

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
    private const int OfflineStaleMinutes = 30;
    private const decimal MovingThresholdKmh = 5m;

    // Speed wins over Ignition OFF (matches frontend resolveFleetStatus) — trackers often
    // report motion without a wired ignition sense, which previously forced false "parked".
    private const string StatusBucketSql = """
        CASE
          WHEN vcl.AlarmType IS NOT NULL AND LOWER(vcl.AlarmType) IN ('sos', 'panic') THEN 'sos'
          WHEN vcl.VehicleId IS NULL THEN
            CASE WHEN v.GpsDeviceId IS NOT NULL THEN 'never_seen' ELSE 'offline' END
          WHEN DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) > @OfflineStaleMinutes THEN 'offline'
          WHEN ISNULL(vcl.Speed, 0) > @MovingThresholdKmh THEN 'moving'
          WHEN vcl.Ignition = 0 THEN 'parked'
          ELSE 'idle'
        END
        """;

    public static async Task<GpsFleetStatusLocalDto> ComputeAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var counts = await connection.QuerySingleAsync<(int Total, int Moving, int Idle, int Parked, int NeverSeen, int Offline, int Sos)>(
            new CommandDefinition(
                $"""
                SELECT
                  COUNT(*) AS Total,
                  ISNULL(SUM(CASE WHEN Bucket = 'moving' THEN 1 ELSE 0 END), 0) AS Moving,
                  ISNULL(SUM(CASE WHEN Bucket = 'idle' THEN 1 ELSE 0 END), 0) AS Idle,
                  ISNULL(SUM(CASE WHEN Bucket = 'parked' THEN 1 ELSE 0 END), 0) AS Parked,
                  ISNULL(SUM(CASE WHEN Bucket = 'never_seen' THEN 1 ELSE 0 END), 0) AS NeverSeen,
                  ISNULL(SUM(CASE WHEN Bucket = 'offline' THEN 1 ELSE 0 END), 0) AS Offline,
                  ISNULL(SUM(CASE WHEN Bucket = 'sos' THEN 1 ELSE 0 END), 0) AS Sos
                FROM (
                  SELECT {StatusBucketSql} AS Bucket
                  FROM Vehicles v
                  LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                  WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
                ) x
                """,
                new { TenantId = tenantId, OfflineStaleMinutes, MovingThresholdKmh },
                cancellationToken: cancellationToken));

        var todayStart = DateTime.UtcNow.Date;
        var alertsToday = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE v.TenantId = @TenantId AND e.IsDeleted = 0 AND e.Timestamp >= @TodayStart
            """,
            new { TenantId = tenantId, TodayStart = todayStart },
            cancellationToken: cancellationToken));

        var online = counts.Moving + counts.Idle + counts.Parked + counts.Sos;

        return new GpsFleetStatusLocalDto(
            counts.Total,
            online,
            counts.Offline,
            counts.Moving,
            counts.Idle,
            counts.Parked,
            counts.NeverSeen,
            counts.Sos,
            alertsToday);
    }
}
