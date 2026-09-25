using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

/// <summary>
/// Local fleet KPI buckets aligned with frontend <c>resolveFleetStatus</c> /
/// <see cref="GpsFleetStatusBuckets"/>.
/// Online = Moving + Idle + Parked + Sos + Unknown.
/// Ignition uses COALESCE(vcl.Ignition, gd.LastIgnition) — same as live fleet.
/// </summary>
public sealed class GpsFleetStatusCalculator : IGpsFleetStatusCalculator
{
    private static string BuildStatusBucketSql(IReadOnlyList<string> sosAlarmValues)
    {
        // Parameterized SOS list: @Sos0, @Sos1, ...
        var sosIn = sosAlarmValues.Count == 0
            ? "1 = 0"
            : string.Join(", ", sosAlarmValues.Select((_, i) => $"@Sos{i}"));

        // Ignition matches live fleet: COALESCE(VCL, device LastIgnition).
        return $"""
            CASE
              WHEN vcl.VehicleId IS NULL THEN
                CASE WHEN v.GpsDeviceId IS NOT NULL THEN 'never_seen' ELSE 'offline' END
              WHEN DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) > @OfflineStaleMinutes THEN 'offline'
              WHEN LOWER(LTRIM(RTRIM(ISNULL(vcl.AlarmType, '')))) IN ({sosIn}) THEN 'sos'
              WHEN COALESCE(vcl.Ignition, gd.LastIgnition) = 0
                   AND ISNULL(vcl.Speed, 0) < @MovingThresholdKmh THEN 'parked'
              WHEN COALESCE(vcl.Ignition, gd.LastIgnition) = 0
                   AND ISNULL(vcl.Speed, 0) >= @MovingThresholdKmh THEN 'unknown'
              WHEN ISNULL(vcl.Speed, 0) >= @MovingThresholdKmh THEN 'moving'
              WHEN COALESCE(vcl.Ignition, gd.LastIgnition) = 1 THEN 'idle'
              ELSE 'unknown'
            END
            """;
    }

    public async Task<GpsFleetStatusLocalDto> ComputeAsync(
        IDbConnection connection,
        int tenantId,
        IOptions<GpsSettings> gpsSettings,
        IOptions<TraccarOptions> traccarOptions,
        CancellationToken cancellationToken = default,
        DataScopeResult? scope = null)
    {
        var staleMinutes = gpsSettings.Value.OfflineStaleMinutes <= 0
            ? GpsFleetStatusBuckets.DefaultOfflineStaleMinutes
            : gpsSettings.Value.OfflineStaleMinutes;
        var movingThresholdKmh = traccarOptions.Value.MovingSpeedKmh > 0
            ? traccarOptions.Value.MovingSpeedKmh
            : gpsSettings.Value.FleetMovingSpeedKmh;

        var sosValues = (traccarOptions.Value.SosAlarmValues ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        if (sosValues.Count == 0)
            sosValues.AddRange(["sos", "panic"]);

        var statusBucketSql = BuildStatusBucketSql(sosValues);

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
        for (var i = 0; i < sosValues.Count; i++)
            parameters.Add($"Sos{i}", sosValues[i]);

        if (scope is not null)
            DataScopeSqlBuilder.ApplyVehicleScope(parameters, scope, "v", vehicleClauses);

        var vehicleWhere = string.Join(" AND ", vehicleClauses);

        var counts = await connection.QuerySingleAsync<(
            int Total, int Moving, int Idle, int Parked, int Sos, int NeverSeen, int Offline, int Unknown)>(
            new CommandDefinition(
                $"""
                SELECT
                  COUNT(*) AS Total,
                  ISNULL(SUM(CASE WHEN Bucket = 'moving' THEN 1 ELSE 0 END), 0) AS Moving,
                  ISNULL(SUM(CASE WHEN Bucket = 'idle' THEN 1 ELSE 0 END), 0) AS Idle,
                  ISNULL(SUM(CASE WHEN Bucket = 'parked' THEN 1 ELSE 0 END), 0) AS Parked,
                  ISNULL(SUM(CASE WHEN Bucket = 'sos' THEN 1 ELSE 0 END), 0) AS Sos,
                  ISNULL(SUM(CASE WHEN Bucket = 'never_seen' THEN 1 ELSE 0 END), 0) AS NeverSeen,
                  ISNULL(SUM(CASE WHEN Bucket = 'offline' THEN 1 ELSE 0 END), 0) AS Offline,
                  ISNULL(SUM(CASE WHEN Bucket = 'unknown' THEN 1 ELSE 0 END), 0) AS Unknown
                FROM (
                  SELECT {statusBucketSql} AS Bucket
                  FROM Vehicles v
                  LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                  LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
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
            DataScopeSqlBuilder.ApplyVehicleScope(alertParams, scope, "v", alertClauses);
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

        var online = counts.Moving + counts.Idle + counts.Parked + counts.Sos + counts.Unknown;

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
