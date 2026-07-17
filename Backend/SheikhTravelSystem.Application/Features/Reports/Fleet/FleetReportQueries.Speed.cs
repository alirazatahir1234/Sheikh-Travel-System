using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public partial class GetFleetReportQueryHandler
{
    /// <summary>
    /// Shows overspeed EVENTS (point-in-time, from GpsAlertEvents) with the vehicle's configured
    /// speed limit where one exists. "Overspeed Duration" from the original spec is intentionally
    /// not included — Phase 8's overspeed alert isn't a bracketed duration, and computing a real one
    /// needs a new consecutive-sample detector (comparable cost to a new trip/stop detector),
    /// out of scope here. Never fabricated as a fake/zero value.
    /// </summary>
    private async Task<ReportResponseDto> BuildSpeedReportAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? driverId, CancellationToken ct)
    {
        var columns = new[]
        {
            new ReportColumnDto("date", "Date", "date"),
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("speed", "Speed (km/h)", "number"),
            new ReportColumnDto("speedLimit", "Speed Limit (km/h)", "number"),
            new ReportColumnDto("latitude", "Latitude", "number"),
            new ReportColumnDto("longitude", "Longitude", "number")
        };

        // EventType filter on GetGpsAlertEventsQuery is an exact DB match, but detectors write
        // spelling variants ("overspeed" vs "speed_exceeded") — fetch unfiltered and normalize in
        // C# via the same GpsEventTypeNormalizer used for Analytics event-family grouping (Phase 10),
        // rather than missing half the real overspeed events.
        var eventsResponse = await mediator.Send(
            new GetGpsAlertEventsQuery(vehicleId, null, from, to, driverId, null, null, null), ct);
        var events = (eventsResponse.Data ?? [])
            .Where(e => GpsEventTypeNormalizer.Normalize(e.EventType) == "overspeed")
            .ToList();

        var speedLimits = (await connection.QueryAsync<(int? VehicleId, decimal SpeedLimitKmh)>(new CommandDefinition("""
            SELECT VehicleId, SpeedLimitKmh FROM GpsAlertRules
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND SpeedLimitKmh IS NOT NULL
            """, new { TenantId = tenantId }, cancellationToken: ct)))
            .Where(r => r.VehicleId.HasValue)
            .ToDictionary(r => r.VehicleId!.Value, r => r.SpeedLimitKmh);

        var rows = events.Select(e => FleetReportHelper.Row(
            e.Id.ToString(), e.VehicleName ?? $"Vehicle #{e.VehicleId}", 1, e.Speed,
            ("date", (object?)e.Timestamp),
            ("vehicle", (object?)(e.VehicleName ?? $"Vehicle #{e.VehicleId}")),
            ("speed", (object?)e.Speed),
            ("speedLimit", (object?)(speedLimits.TryGetValue(e.VehicleId, out var limit) ? (object)limit : "—")),
            ("latitude", (object?)e.Latitude),
            ("longitude", (object?)e.Longitude)))
            .ToList();

        var summary = new Dictionary<string, object?>
        {
            ["overspeedEvents"] = rows.Count,
            ["maxSpeed"] = rows.Count > 0 ? rows.Max(r => r.TotalValue) : (object?)null,
            ["avgSpeed"] = rows.Count > 0 ? Math.Round(rows.Average(r => r.TotalValue), 1) : (object?)null
        };

        return new ReportResponseDto("speed", FleetReportHelper.TitleFor("speed"), columns, rows, 0, summary);
    }
}
