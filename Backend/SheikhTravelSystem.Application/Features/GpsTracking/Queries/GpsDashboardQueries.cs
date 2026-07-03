using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetGpsDashboardSummaryQuery : IRequest<ApiResponse<GpsDashboardSummaryDto>>;

public class GetGpsDashboardSummaryQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<GetGpsDashboardSummaryQuery, ApiResponse<GpsDashboardSummaryDto>>
{
    private const double MovingSpeedKmh = 5;
    private static readonly TimeSpan OfflineStale = TimeSpan.FromMinutes(30);

    private sealed class VehicleRow
    {
        public int Id { get; init; }
        public string? Status { get; init; }
        public bool HasGpsDevice { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public decimal? Speed { get; init; }
        public bool? Ignition { get; init; }
        public DateTime? LastUpdate { get; init; }
        public string? AlarmType { get; init; }
        public double? LocLat { get; init; }
        public double? LocLng { get; init; }
        public DateTime? LocLastUpdate { get; init; }
        public bool? LocIgnition { get; init; }
    }

    public async Task<ApiResponse<GpsDashboardSummaryDto>> Handle(
        GetGpsDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var now = DateTime.UtcNow;

        var rows = (await connection.QueryAsync<VehicleRow>(new CommandDefinition(
            """
            SELECT v.Id,
                   v.Status,
                   CASE WHEN gd.Id IS NOT NULL THEN 1 ELSE 0 END AS HasGpsDevice,
                   vcl.Latitude,
                   vcl.Longitude,
                   vcl.Speed,
                   vcl.Ignition,
                   vcl.LastUpdate,
                   vcl.AlarmType,
                   v.LocationLatitude AS LocLat,
                   v.LocationLongitude AS LocLng,
                   v.LocationLastUpdate AS LocLastUpdate,
                   v.EngineIgnition AS LocIgnition
            FROM Vehicles v
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            LEFT JOIN GpsDevices gd ON gd.VehicleId = v.Id AND gd.IsDeleted = 0
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Status <> 'Retired'
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken))).ToList();

        var moving = 0;
        var idle = 0;
        var parked = 0;
        var offline = 0;
        var neverSeen = 0;
        var online = 0;

        foreach (var row in rows)
        {
            var bucket = Classify(row, now);
            switch (bucket)
            {
                case "moving": moving++; online++; break;
                case "idle": idle++; online++; break;
                case "parked": parked++; online++; break;
                case "offline": offline++; break;
                case "never_seen": neverSeen++; break;
            }
        }

        var todayStart = now.Date;
        var alertsToday = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*) FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            WHERE e.IsDeleted = 0 AND e.Timestamp >= @TodayStart
            """,
            new { TenantId = tenantId, TodayStart = todayStart },
            cancellationToken: cancellationToken));

        var sparkMoving = new int[7];
        var sparkParked = new int[7];
        var sparkIdle = new int[7];
        var sparkOffline = new int[7];
        for (var i = 0; i < 7; i++)
        {
            var day = todayStart.AddDays(-6 + i);
            var next = day.AddDays(1);
            var dayCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(DISTINCT gp.VehicleId)
                FROM GpsPositions gp
                INNER JOIN Vehicles v ON v.Id = gp.VehicleId AND v.TenantId = @TenantId
                WHERE gp.RecordedAt >= @DayStart AND gp.RecordedAt < @DayEnd
                  AND gp.Speed > @MovingSpeed
                """,
                new { TenantId = tenantId, DayStart = day, DayEnd = next, MovingSpeed = MovingSpeedKmh },
                cancellationToken: cancellationToken));
            sparkMoving[i] = dayCount;
            sparkParked[i] = Math.Max(0, parked / 7);
            sparkIdle[i] = Math.Max(0, idle / 7);
            sparkOffline[i] = Math.Max(0, offline / 7);
        }

        var zeroTrend = 0d;
        var summary = new GpsDashboardSummaryDto(
            online,
            moving,
            parked,
            idle,
            offline,
            neverSeen,
            rows.Count,
            alertsToday,
            new GpsDashboardTrendsDto(zeroTrend, zeroTrend, zeroTrend, zeroTrend, zeroTrend, zeroTrend, zeroTrend, zeroTrend),
            new GpsDashboardSparklineDto(sparkMoving, sparkParked, sparkIdle, sparkOffline),
            now);

        return ApiResponse<GpsDashboardSummaryDto>.SuccessResponse(summary);
    }

    private static string Classify(VehicleRow row, DateTime now)
    {
        if (!string.IsNullOrEmpty(row.AlarmType) &&
            (row.AlarmType.Equals("sos", StringComparison.OrdinalIgnoreCase) ||
             row.AlarmType.Equals("panic", StringComparison.OrdinalIgnoreCase)))
        {
            return "moving";
        }

        var hasCoords = row.Latitude is not null && row.Longitude is not null &&
                        !(row.Latitude == 0 && row.Longitude == 0);
        if (hasCoords && row.LastUpdate is not null)
        {
            return ClassifyTelemetry(row.Speed, row.Ignition, row.LastUpdate.Value, now);
        }

        var hasSnapshot = row.LocLat is not null && row.LocLng is not null &&
                          !(row.LocLat == 0 && row.LocLng == 0);
        if (hasSnapshot && row.LocLastUpdate is not null)
        {
            return ClassifyTelemetry(0, row.LocIgnition, row.LocLastUpdate.Value, now);
        }

        if (row.HasGpsDevice)
        {
            return "never_seen";
        }

        return "offline";
    }

    private static string ClassifyTelemetry(decimal? speed, bool? ignition, DateTime lastUpdate, DateTime now)
    {
        if (now - lastUpdate > OfflineStale)
        {
            return "offline";
        }

        var speedKmh = (double)(speed ?? 0);
        if (ignition == false)
        {
            return "parked";
        }

        return speedKmh > MovingSpeedKmh ? "moving" : "idle";
    }
}
