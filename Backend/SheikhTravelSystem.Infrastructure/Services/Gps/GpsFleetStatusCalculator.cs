using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsFleetStatusCalculator : IGpsFleetStatusCalculator
{
    private const string StatusBucketSql = """
        CASE
          WHEN vcl.VehicleId IS NULL THEN
            CASE WHEN v.GpsDeviceId IS NOT NULL THEN 'never_seen' ELSE 'offline' END
          WHEN DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) > @OfflineStaleMinutes THEN 'offline'
          WHEN ISNULL(vcl.Speed, 0) >= @MovingThresholdKmh THEN 'moving'
          ELSE 'idle'
        END
        """;

    public async Task<GpsFleetStatusLocalDto> ComputeAsync(
        IDbConnection connection,
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
            DataScopeSqlBuilder.ApplyVehicleScope(parameters, scope, "v", vehicleClauses);

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
