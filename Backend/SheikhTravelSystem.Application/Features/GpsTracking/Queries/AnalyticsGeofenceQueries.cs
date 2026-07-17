using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetGeofenceAnalyticsQuery(DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<GeofenceAnalyticsDto>>;

/// <summary>
/// Extends the (now tenant-fixed) GetGeofenceStatsQuery with a most/least-visited ranking and dwell
/// time — dwell is derived by pairing consecutive geofence_enter → geofence_exit events per
/// vehicle+geofence in the range, not a stored duration.
/// </summary>
public class GetGeofenceAnalyticsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetGeofenceAnalyticsQuery, ApiResponse<GeofenceAnalyticsDto>>
{
    public async Task<ApiResponse<GeofenceAnalyticsDto>> Handle(GetGeofenceAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var events = (await connection.QueryAsync<(int GeofenceId, string GeofenceName, int VehicleId, string EventType, DateTime Timestamp)>(
            new CommandDefinition(
                """
                SELECT e.GeofenceId, g.Name AS GeofenceName, e.VehicleId, e.EventType, e.Timestamp
                FROM GpsAlertEvents e
                INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
                INNER JOIN Geofences g ON g.Id = e.GeofenceId
                WHERE e.EventType IN ('geofence_enter', 'geofence_exit') AND e.IsDeleted = 0
                  AND e.GeofenceId IS NOT NULL AND e.Timestamp BETWEEN @FromDate AND @ToDate
                ORDER BY e.VehicleId, e.GeofenceId, e.Timestamp
                """,
                new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        var dwellMinutesByGeofence = new Dictionary<int, List<decimal>>();
        foreach (var group in events.GroupBy(e => (e.VehicleId, e.GeofenceId)))
        {
            var ordered = group.OrderBy(e => e.Timestamp).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                if (ordered[i].EventType != "geofence_enter" || ordered[i + 1].EventType != "geofence_exit")
                    continue;

                var minutes = (decimal)(ordered[i + 1].Timestamp - ordered[i].Timestamp).TotalMinutes;
                if (minutes <= 0) continue;

                if (!dwellMinutesByGeofence.TryGetValue(group.Key.GeofenceId, out var list))
                {
                    list = [];
                    dwellMinutesByGeofence[group.Key.GeofenceId] = list;
                }
                list.Add(minutes);
            }
        }

        var byGeofence = events
            .GroupBy(e => new { e.GeofenceId, e.GeofenceName })
            .Select(g => new GeofenceVisitDto(
                g.Key.GeofenceId,
                g.Key.GeofenceName,
                g.Count(e => e.EventType == "geofence_enter"),
                g.Count(e => e.EventType == "geofence_exit"),
                dwellMinutesByGeofence.TryGetValue(g.Key.GeofenceId, out var dwell) && dwell.Count > 0
                    ? Math.Round(dwell.Average(), 1)
                    : null))
            .OrderByDescending(g => g.EntryCount + g.ExitCount)
            .ToList();

        var dto = new GeofenceAnalyticsDto(
            byGeofence.Take(10).ToList(),
            byGeofence.AsEnumerable().Reverse().Take(10).ToList(),
            events.Count(e => e.EventType == "geofence_enter"),
            events.Count(e => e.EventType == "geofence_exit"));

        return ApiResponse<GeofenceAnalyticsDto>.SuccessResponse(dto);
    }
}

public record GetAlertEventStatsQuery(DateTime? FromDate, DateTime? ToDate, int? VehicleId = null)
    : IRequest<ApiResponse<AlertEventStatsDto>>;

/// <summary>Thin wrapper — same filter/tenant-scoping shape as the (now tenant-fixed) GetGpsAlertEventsQuery, plus a GROUP BY through GpsEventTypeNormalizer.</summary>
public class GetAlertEventStatsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetAlertEventStatsQuery, ApiResponse<AlertEventStatsDto>>
{
    public async Task<ApiResponse<AlertEventStatsDto>> Handle(GetAlertEventStatsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var sql = """
            SELECT e.EventType, e.Severity, e.Timestamp
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.IsDeleted = 0 AND e.Timestamp BETWEEN @FromDate AND @ToDate
            """;

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);
        parameters.Add("FromDate", fromDate);
        parameters.Add("ToDate", toDate);

        if (request.VehicleId.HasValue)
        {
            sql += " AND e.VehicleId = @VehicleId";
            parameters.Add("VehicleId", request.VehicleId.Value);
        }

        var events = (await connection.QueryAsync<(string EventType, string Severity, DateTime Timestamp)>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        var byType = events
            .GroupBy(e => GpsEventTypeNormalizer.Normalize(e.EventType))
            .Select(g => new EventTypeCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var bySeverity = events
            .GroupBy(e => e.Severity)
            .Select(g => new EventTypeCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var daily = events
            .GroupBy(e => e.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyEventCountDto(g.Key, g.Count()))
            .ToList();

        var dto = new AlertEventStatsDto(byType, bySeverity, daily, events.Count);
        return ApiResponse<AlertEventStatsDto>.SuccessResponse(dto);
    }
}
