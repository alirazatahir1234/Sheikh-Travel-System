using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public partial class GetFleetReportQueryHandler
{
    private async Task<ReportResponseDto> BuildDriverReportAsync(
        int tenantId, DateTime from, DateTime to,
        int? branchId, int? departmentId, CancellationToken ct)
    {
        var columns = new[]
        {
            new ReportColumnDto("name", "Driver Name", "text"),
            new ReportColumnDto("license", "License", "text"),
            new ReportColumnDto("phone", "Phone", "text"),
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("totalTrips", "Total Trips", "number"),
            new ReportColumnDto("distance", "Distance (km)", "number"),
            new ReportColumnDto("drivingHours", "Driving Hours", "number"),
            new ReportColumnDto("idleHours", "Idle Hours", "number"),
            new ReportColumnDto("driverScore", "Driver Score", "number"),
            new ReportColumnDto("licenseExpiry", "License Expiry", "date")
        };

        var profiles = (await reportRepository.GetDriverProfilesAsync(
            tenantId, branchId, departmentId, ct)).ToDictionary(d => d.Id, d => d);

        if (profiles.Count == 0)
        {
            return new ReportResponseDto("driver", FleetReportHelper.TitleFor("driver"), columns, [], 0,
                new Dictionary<string, object?> { ["totalDrivers"] = 0 });
        }

        // Composite analytics (Trips/Distance/IdleMinutes/Score/Rating) reuse the existing driver
        // scoring engine (Phase 10) — same AssignmentHistory-attributed trips, not recomputed here.
        var scoresResponse = await mediator.Send(new GetDriverScoreRankingQuery(from, to, branchId, departmentId), ct);
        var scores = (scoresResponse.Data ?? []).ToDictionary(s => s.DriverId, s => s);

        // Driving Hours isn't on DriverScoreDto (the scoring engine only needs distance/idle, not
        // duration) — resolved separately here via the same trip+AssignmentHistory attribution the
        // scoring engine itself uses (GetDriverScoreRankingQueryHandler), copied rather than shared,
        // per this phase's mirroring convention.
        var drivingMinutesByDriver = await ResolveDrivingMinutesByDriverAsync(tenantId, from, to, branchId, departmentId, ct);

        // Current vehicle assignment per driver (latest Active AssignmentHistory row).
        var currentVehicles = await reportRepository.GetActiveAssignmentVehicleNamesAsync(
            tenantId, profiles.Keys.ToList(), ct);

        var rows = profiles.Values.Select(d =>
        {
            scores.TryGetValue(d.Id, out var score);
            var driveMinutes = drivingMinutesByDriver.GetValueOrDefault(d.Id);
            return FleetReportHelper.Row(
                d.Id.ToString(), d.FullName, score?.Factors.TripCount ?? 0, score?.Factors.DistanceKm ?? 0m,
                ("name", (object?)d.FullName),
                ("license", (object?)d.LicenseNumber),
                ("phone", (object?)d.Phone),
                ("vehicle", (object?)(currentVehicles.GetValueOrDefault(d.Id) ?? "—")),
                ("totalTrips", (object?)(score?.Factors.TripCount ?? 0)),
                ("distance", (object?)(score?.Factors.DistanceKm ?? 0m)),
                ("drivingHours", (object?)Math.Round(driveMinutes / 60m, 1)),
                ("idleHours", (object?)Math.Round((score?.Factors.IdleMinutes ?? 0) / 60m, 1)),
                ("driverScore", (object?)(score?.Score)),
                ("licenseExpiry", (object?)d.LicenseExpiryDate));
        }).ToList();

        var summary = new Dictionary<string, object?>
        {
            ["totalDrivers"] = rows.Count,
            ["active"] = scores.Count
        };

        return new ReportResponseDto("driver", FleetReportHelper.TitleFor("driver"), columns, rows,
            rows.Sum(r => r.TotalValue), summary);
    }

    private async Task<Dictionary<int, int>> ResolveDrivingMinutesByDriverAsync(
        int tenantId, DateTime from, DateTime to,
        int? branchId, int? departmentId, CancellationToken ct)
    {
        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, from, to, branchId, departmentId, null, Unpaged: true), ct);
        var trips = tripsResponse.Data?.Items ?? [];
        if (trips.Count == 0) return [];

        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();
        var assignments = await reportRepository.GetAssignmentWindowsAsync(
            tenantId, vehicleIds, from, to, ct);

        int? ResolveDriver(int vehicleId, DateTime at) =>
            assignments.FirstOrDefault(a => a.VehicleId == vehicleId && a.StartAt <= at && (a.EndAt == null || a.EndAt >= at)).DriverId;

        var result = new Dictionary<int, int>();
        foreach (var t in trips)
        {
            var driverId = ResolveDriver(t.VehicleId, t.StartTime);
            if (!driverId.HasValue) continue;
            result[driverId.Value] = result.GetValueOrDefault(driverId.Value) + t.DurationMinutes;
        }
        return result;
    }
}
