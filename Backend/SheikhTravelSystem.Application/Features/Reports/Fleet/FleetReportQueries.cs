using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public record GetFleetReportQuery(
    string ReportType,
    DateTime? From = null,
    DateTime? To = null,
    int? VehicleId = null,
    int? DriverId = null,
    int? BranchId = null,
    int? DepartmentId = null,
    string? Status = null,
    /// <summary>Only used when ReportType is "maintenance" — selects one of the existing Maintenance
    /// Reports sub-types (cost-analysis, vehicle-maintenance, service-due, etc). See
    /// GetMaintenanceReportQuery for the full list; defaults the same way it does.</summary>
    string? MaintenanceReportType = null)
    : IRequest<ApiResponse<ReportResponseDto>>;

/// <summary>
/// Dispatches to per-report-type builders (split across FleetReportQueries.*.cs partial files, one
/// report family per file, mirroring the GpsTrackingController.Analytics.cs partial-file precedent).
/// Every builder returns the same self-describing ReportResponseDto shape — see
/// Common/ReportDtos.cs — so the frontend renders/exports any report generically.
/// </summary>
public partial class GetFleetReportQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITenantContext tenantContext,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetFleetReportQuery, ApiResponse<ReportResponseDto>>
{
    public async Task<ApiResponse<ReportResponseDto>> Handle(GetFleetReportQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var reportType = FleetReportHelper.NormalizeReportType(request.ReportType);
        var (from, to) = FleetReportHelper.ResolveDateRange(request.From, request.To);
        using var connection = dbFactory.CreateConnection();

        DataScopeResult? scope = null;
        if (currentUser.UserId is int userId)
        {
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            if (!DataScopeSql.TryIntersectOptional(scope, request.BranchId, request.DepartmentId, out _, out _, out var scopeError))
                return ApiResponse<ReportResponseDto>.FailResponse(scopeError ?? "Outside data scope.");
        }

        // Expanded day by day — each report type's builder lands in its own FleetReportQueries.*.cs
        // partial file as it's implemented (see the Phase 11 plan). Unrecognized/not-yet-built types
        // fall back to Trip, same default-case convention as GetMaintenanceReportQueryHandler.
        var report = reportType switch
        {
            "event" => await BuildEventReportAsync(connection, tenantId, from, to, request.VehicleId,
                request.DriverId, request.BranchId, request.DepartmentId, cancellationToken),
            "alert" => await BuildAlertReportAsync(connection, tenantId, from, to, request.VehicleId,
                request.DriverId, request.BranchId, request.DepartmentId, request.Status, cancellationToken),
            "vehicle" => await BuildVehicleReportAsync(connection, tenantId, request.BranchId,
                request.DepartmentId, request.Status, cancellationToken, scope),
            "driver" => await BuildDriverReportAsync(connection, tenantId, from, to, request.BranchId,
                request.DepartmentId, cancellationToken),
            "fuel" => await BuildFuelReportAsync(connection, tenantId, from, to, request.VehicleId,
                request.BranchId, cancellationToken, scope),
            "speed" => await BuildSpeedReportAsync(connection, tenantId, from, to, request.VehicleId,
                request.DriverId, cancellationToken),
            "idle" => await BuildIdleReportAsync(tenantId, from, to, request.VehicleId, cancellationToken),
            "stop" => await BuildStopReportAsync(tenantId, from, to, request.VehicleId, cancellationToken),
            "maintenance" => await BuildMaintenanceReportAsync(request.MaintenanceReportType, from, to,
                request.VehicleId, request.BranchId, request.Status, cancellationToken),
            _ => await BuildTripReportAsync(from, to, request.VehicleId, request.DriverId,
                request.BranchId, request.DepartmentId, cancellationToken)
        };

        return ApiResponse<ReportResponseDto>.SuccessResponse(report);
    }

    private async Task<ReportResponseDto> BuildTripReportAsync(
        DateTime from, DateTime to,
        int? vehicleId, int? driverId, int? branchId, int? departmentId, CancellationToken ct)
    {
        var columns = new[]
        {
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("driver", "Driver", "text"),
            new ReportColumnDto("tracker", "Tracker", "text"),
            new ReportColumnDto("tripDate", "Trip Date", "date"),
            new ReportColumnDto("startTime", "Start Time", "date"),
            new ReportColumnDto("endTime", "End Time", "date"),
            new ReportColumnDto("duration", "Duration (min)", "number"),
            new ReportColumnDto("distance", "Distance (km)", "number"),
            new ReportColumnDto("avgSpeed", "Avg Speed (km/h)", "number"),
            new ReportColumnDto("maxSpeed", "Max Speed (km/h)", "number"),
            new ReportColumnDto("fuel", "Fuel (L)", "number"),
            new ReportColumnDto("startAddress", "Start Address", "text"),
            new ReportColumnDto("endAddress", "End Address", "text")
        };

        // Per-trip Idle Time/Engine Hours aren't on GpsTripDto (no per-trip breakdown exists) —
        // surfaced only in Summary below via the fleet-wide trip summary, never fabricated per-row.
        var tripsResponse = await mediator.Send(new GetGpsTripsQuery(
            vehicleId, from, to, branchId, departmentId, driverId, Unpaged: true), ct);
        var trips = tripsResponse.Data?.Items ?? [];

        var rows = trips.Select(t => FleetReportHelper.Row(
            t.TripKey ?? $"{t.VehicleId}-{t.StartTime:O}", t.VehicleName ?? $"Vehicle #{t.VehicleId}", 1, (decimal)t.DistanceKm,
            ("vehicle", (object?)(t.VehicleName ?? $"Vehicle #{t.VehicleId}")),
            ("driver", (object?)(t.DriverName ?? "—")),
            ("tracker", (object?)(t.DeviceName ?? "—")),
            ("tripDate", (object?)t.StartTime.Date),
            ("startTime", (object?)t.StartTime),
            ("endTime", (object?)t.EndTime),
            ("duration", (object?)t.DurationMinutes),
            ("distance", (object?)t.DistanceKm),
            ("avgSpeed", (object?)t.AvgSpeedKmh),
            ("maxSpeed", (object?)t.MaxSpeedKmh),
            ("fuel", (object?)t.FuelLiters),
            ("startAddress", (object?)(t.StartAddress ?? "—")),
            ("endAddress", (object?)(t.EndAddress ?? "—"))))
            .ToList();

        var totalDistanceKm = rows.Sum(r => r.TotalValue);
        var summary = new Dictionary<string, object?>
        {
            ["tripCount"] = rows.Count,
            ["totalDistanceKm"] = totalDistanceKm
        };

        // Idle/engine-hour totals only make sense as a fleet-wide figure (no per-trip source exists) —
        // only surfaced when the report isn't scoped to one vehicle, so a single-vehicle report never
        // shows a fleet-wide number mislabeled as vehicle-specific.
        if (vehicleId is null)
        {
            var fleetSummaryResponse = await mediator.Send(
                new GetFleetTripSummaryQuery(from, to, branchId, departmentId, driverId), ct);
            if (fleetSummaryResponse.Data is { } fs)
            {
                summary["totalIdleMinutes"] = fs.IdleMinutes;
                summary["totalEngineHours"] = fs.EngineHours;
                summary["totalDriveMinutes"] = fs.DrivingMinutes;
            }
        }

        return new ReportResponseDto("trip", FleetReportHelper.TitleFor("trip"), columns, rows, totalDistanceKm, summary);
    }

    private async Task<ReportResponseDto> BuildEventReportAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? driverId, int? branchId, int? departmentId, CancellationToken ct)
    {
        var columns = new[]
        {
            new ReportColumnDto("time", "Time", "date"),
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("eventType", "Event Type", "text"),
            new ReportColumnDto("driver", "Driver", "text"),
            new ReportColumnDto("location", "Location", "text"),
            new ReportColumnDto("severity", "Severity", "text")
        };

        var eventsResponse = await mediator.Send(
            new GetGpsAlertEventsQuery(vehicleId, null, from, to, driverId, null, null, null), ct);
        var events = eventsResponse.Data ?? [];

        events = await FilterByBranchDepartmentAsync(connection, tenantId, events, branchId, departmentId, ct);

        var rows = events.Select(e => FleetReportHelper.Row(
            e.Id.ToString(), e.EventType, 1, 0m,
            ("time", (object?)e.Timestamp),
            ("vehicle", (object?)(e.VehicleName ?? $"Vehicle #{e.VehicleId}")),
            ("eventType", (object?)e.EventType),
            ("driver", (object?)(e.DriverName ?? "—")),
            ("location", (object?)$"{e.Latitude:F4}, {e.Longitude:F4}"),
            ("severity", (object?)e.Severity)))
            .ToList();

        return new ReportResponseDto("event", FleetReportHelper.TitleFor("event"), columns, rows, 0,
            new Dictionary<string, object?> { ["totalEvents"] = rows.Count });
    }

    private async Task<ReportResponseDto> BuildAlertReportAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? driverId, int? branchId, int? departmentId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new ReportColumnDto("alertName", "Alert Name", "text"),
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("driver", "Driver", "text"),
            new ReportColumnDto("date", "Date", "date"),
            new ReportColumnDto("severity", "Severity", "text"),
            new ReportColumnDto("status", "Status", "text"),
            new ReportColumnDto("actionTaken", "Action Taken", "text")
        };

        var eventsResponse = await mediator.Send(
            new GetGpsAlertEventsQuery(vehicleId, null, from, to, driverId, null, null, status), ct);
        var events = eventsResponse.Data ?? [];

        events = await FilterByBranchDepartmentAsync(connection, tenantId, events, branchId, departmentId, ct);

        var rows = events.Select(e => FleetReportHelper.Row(
            e.Id.ToString(), e.EventType, 1, 0m,
            ("alertName", (object?)e.EventType),
            ("vehicle", (object?)(e.VehicleName ?? $"Vehicle #{e.VehicleId}")),
            ("driver", (object?)(e.DriverName ?? "—")),
            ("date", (object?)e.Timestamp),
            ("severity", (object?)e.Severity),
            ("status", (object?)e.Status),
            ("actionTaken", (object?)(e.ResolutionNotes ?? e.ResolvedBy ?? e.AcknowledgedBy ?? "—"))))
            .ToList();

        return new ReportResponseDto("alert", FleetReportHelper.TitleFor("alert"), columns, rows, 0,
            new Dictionary<string, object?>
            {
                ["totalAlerts"] = rows.Count,
                ["open"] = events.Count(e => !e.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase)
                    && !e.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase)),
                ["closed"] = events.Count(e => e.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase)
                    || e.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            });
    }

    /// <summary>GetGpsAlertEventsQuery has no Branch/Department filter — pre-resolve the matching VehicleId set and post-filter in C# when either is supplied.</summary>
    private static async Task<List<GpsAlertEventDto>> FilterByBranchDepartmentAsync(
        System.Data.IDbConnection connection, int tenantId, List<GpsAlertEventDto> events,
        int? branchId, int? departmentId, CancellationToken ct)
    {
        if (branchId is null && departmentId is null) return events;

        var clauses = new List<string> { "TenantId = @TenantId", "IsDeleted = 0" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (branchId.HasValue) { clauses.Add("BranchId = @BranchId"); p.Add("BranchId", branchId.Value); }
        if (departmentId.HasValue) { clauses.Add("DepartmentId = @DepartmentId"); p.Add("DepartmentId", departmentId.Value); }

        var matchingIds = (await connection.QueryAsync<int>(new CommandDefinition(
            $"SELECT Id FROM Vehicles WHERE {string.Join(" AND ", clauses)}", p, cancellationToken: ct))).ToHashSet();

        return events.Where(e => matchingIds.Contains(e.VehicleId)).ToList();
    }
}
