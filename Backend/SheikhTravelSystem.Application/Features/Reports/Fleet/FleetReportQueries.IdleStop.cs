using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

/// <summary>
/// Idle and Stop reports are both sourced from Traccar's per-device stops endpoint (this codebase
/// has no separate "idle" detector — Idle/Stop analytics, Phase 10, already draw from the exact same
/// ITraccarClient.GetStopsAsync data, just aggregated differently). Row-level here instead of the
/// analytics' top-10/fleet-summary aggregation. Same uncapped per-device Traccar fan-out cost profile
/// already accepted for GetIdleAnalyticsQuery/GetStopAnalyticsQuery — extended to row-level output,
/// not a new architectural risk.
/// </summary>
public partial class GetFleetReportQueryHandler
{
    private async Task<(List<TraccarStopRow> Stops, bool IsPartial)> FetchStopsAsync(
        int tenantId, DateTime from, DateTime to, int? vehicleId, CancellationToken ct)
    {
        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(vehicleId, from, to, null, null, null, Unpaged: true), ct);
        var trips = tripsResponse.Data?.Items ?? [];
        var vehicleNames = trips.GroupBy(t => t.VehicleId).ToDictionary(g => g.Key, g => g.First().VehicleName);

        var opts = traccarOptions.Value;
        if (!opts.IsConfigured || !opts.Enabled || trips.Count == 0)
            return ([], trips.Count > 0);

        using var connection = dbFactory.CreateConnection();
        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();

        var deviceMap = await GpsTraccarFleetFetcher.ResolveVehicleToDeviceMapAsync(connection, tenantId, vehicleIds, ct);
        var isPartial = await GpsTraccarFleetFetcher.HasNonTraccarVehicleAsync(connection, tenantId, vehicleIds, ct);
        if (deviceMap.Count == 0) return ([], true);

        // Driver attribution via the same AssignmentHistory time-window join used elsewhere this
        // phase (Driver Report) and in Phase 10's driver scoring — copied, not shared.
        var assignments = (await connection.QueryAsync<(int VehicleId, int? DriverId, DateTime StartAt, DateTime? EndAt)>(
            new CommandDefinition("""
                SELECT VehicleId, DriverId, StartAt, EndAt FROM AssignmentHistory
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId IS NOT NULL
                  AND VehicleId IN @VehicleIds AND StartAt <= @To AND (EndAt IS NULL OR EndAt >= @From)
                """, new { TenantId = tenantId, VehicleIds = vehicleIds, From = from, To = to }, cancellationToken: ct))).ToList();
        var driverNames = (await connection.QueryAsync<(int Id, string FullName)>(new CommandDefinition(
            "SELECT Id, FullName FROM Drivers WHERE TenantId = @TenantId AND IsDeleted = 0",
            new { TenantId = tenantId }, cancellationToken: ct))).ToDictionary(d => d.Id, d => d.FullName);

        int? ResolveDriver(int vId, DateTime at) =>
            assignments.FirstOrDefault(a => a.VehicleId == vId && a.StartAt <= at && (a.EndAt == null || a.EndAt >= at)).DriverId;

        var deviceToVehicle = deviceMap.ToDictionary(kv => kv.Value, kv => kv.Key);
        var stopsTasks = deviceMap.Values.Select(id => traccarClient.GetStopsAsync(id, from, to, ct));
        var allStops = (await Task.WhenAll(stopsTasks)).SelectMany(s => s).ToList();

        var rows = new List<TraccarStopRow>();
        foreach (var stop in allStops)
        {
            if (!deviceToVehicle.TryGetValue(stop.DeviceId, out var vId)) continue;
            var driverId = ResolveDriver(vId, stop.StartTime);
            var minutes = stop.Duration >= 100_000 ? stop.Duration / 60_000 : Math.Max(1, stop.Duration / 60);
            rows.Add(new TraccarStopRow(vId, vehicleNames.GetValueOrDefault(vId) ?? $"Vehicle #{vId}",
                driverId.HasValue ? driverNames.GetValueOrDefault(driverId.Value) : null,
                stop.StartTime, stop.EndTime, minutes, stop.Address ?? $"{stop.Lat:F4}, {stop.Lon:F4}"));
        }

        return (rows, isPartial);
    }

    private async Task<ReportResponseDto> BuildIdleReportAsync(
        int tenantId, DateTime from, DateTime to, int? vehicleId, CancellationToken ct)
    {
        var columns = new[]
        {
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("driver", "Driver", "text"),
            new ReportColumnDto("start", "Start", "date"),
            new ReportColumnDto("end", "End", "date"),
            new ReportColumnDto("idleDuration", "Idle Duration (min)", "number"),
            new ReportColumnDto("location", "Location", "text")
        };

        var (stops, isPartial) = await FetchStopsAsync(tenantId, from, to, vehicleId, ct);

        var rows = stops.Select(s => FleetReportHelper.Row(
            $"{s.VehicleId}-{s.Start:O}", s.VehicleName, 1, s.DurationMinutes,
            ("vehicle", (object?)s.VehicleName),
            ("driver", (object?)(s.DriverName ?? "—")),
            ("start", (object?)s.Start),
            ("end", (object?)s.End),
            ("idleDuration", (object?)s.DurationMinutes),
            ("location", (object?)s.Location)))
            .ToList();

        var summary = new Dictionary<string, object?>
        {
            ["totalIdleMinutes"] = rows.Sum(r => r.TotalValue),
            ["longestIdleMinutes"] = rows.Count > 0 ? rows.Max(r => r.TotalValue) : 0,
            ["averageIdleMinutes"] = rows.Count > 0 ? Math.Round(rows.Average(r => r.TotalValue), 1) : 0,
            ["isPartial"] = isPartial
        };

        return new ReportResponseDto("idle", FleetReportHelper.TitleFor("idle"), columns, rows,
            rows.Sum(r => r.TotalValue), summary);
    }

    private async Task<ReportResponseDto> BuildStopReportAsync(
        int tenantId, DateTime from, DateTime to, int? vehicleId, CancellationToken ct)
    {
        var columns = new[]
        {
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("driver", "Driver", "text"),
            new ReportColumnDto("arrival", "Arrival", "date"),
            new ReportColumnDto("departure", "Departure", "date"),
            new ReportColumnDto("stopDuration", "Stop Duration (min)", "number"),
            new ReportColumnDto("address", "Address", "text")
        };

        var (stops, isPartial) = await FetchStopsAsync(tenantId, from, to, vehicleId, ct);

        var rows = stops.Select(s => FleetReportHelper.Row(
            $"{s.VehicleId}-{s.Start:O}", s.VehicleName, 1, s.DurationMinutes,
            ("vehicle", (object?)s.VehicleName),
            ("driver", (object?)(s.DriverName ?? "—")),
            ("arrival", (object?)s.Start),
            ("departure", (object?)s.End),
            ("stopDuration", (object?)s.DurationMinutes),
            ("address", (object?)s.Location)))
            .ToList();

        var summary = new Dictionary<string, object?>
        {
            ["totalStops"] = rows.Count,
            ["averageStopMinutes"] = rows.Count > 0 ? Math.Round(rows.Average(r => r.TotalValue), 1) : 0,
            ["isPartial"] = isPartial
        };

        return new ReportResponseDto("stop", FleetReportHelper.TitleFor("stop"), columns, rows, 0, summary);
    }
}

internal sealed record TraccarStopRow(
    int VehicleId, string VehicleName, string? DriverName,
    DateTime Start, DateTime End, int DurationMinutes, string Location);
