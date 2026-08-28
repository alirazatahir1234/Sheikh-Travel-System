using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Application.Features.Tracking.Commands;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;
using SheikhTravelSystem.Application.Features.Tracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(AnalyticsPermissions.GpsView)]
/// <summary>
/// GPS fleet tracking — live positions, history, trips, geofences, alerts, devices, commands, and
/// analytics. Analytics routes live in the GpsTrackingController.Analytics.cs partial to keep this
/// file from growing past a screenful of unrelated resource areas.
/// </summary>
[Route("api/gps")]
public partial class GpsTrackingController : BaseApiController
{
    [HttpPost("positions")]
    [RequirePermission(AnalyticsPermissions.GpsView)]
    public async Task<IActionResult> IngestPosition([FromBody] IngestPositionDto position)
        => Ok(await Mediator.Send(new IngestPositionCommand(position)));

    [HttpGet("live")]
    public async Task<IActionResult> GetLive([FromQuery] int page = 1, [FromQuery] int pageSize = 500)
        => Ok(await Mediator.Send(new GetLivePositionsQuery(page, pageSize)));

    /// <summary>
    /// Live-map vehicle roster (names, plates, last fix, tracker). Requires GPS.View only —
    /// does not depend on Vehicle.View so fleet managers / GPS operators can populate the grid.
    /// </summary>
    [HttpGet("live/fleet")]
    public async Task<IActionResult> GetLiveFleet()
        => Ok(await Mediator.Send(new GetGpsLiveFleetQuery()));

    /// <summary>On-demand reverse geocode (cache-first via Nominatim).</summary>
    [HttpGet("location/reverse")]
    public async Task<IActionResult> ReverseGeocode(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] bool forceRefresh = false)
        => Ok(await Mediator.Send(new ReverseGeocodeQuery(lat, lng, forceRefresh)));

    [HttpGet("history/{vehicleId:int}")]
    public async Task<IActionResult> GetHistory(int vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetPositionHistoryQuery(vehicleId, from, to)));

    [HttpGet("history/replay")]
    public async Task<IActionResult> GetHistoryReplay(
        [FromQuery] int? vehicleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? routeMaxPoints,
        [FromQuery] int? playbackMaxPoints,
        [FromQuery] bool includeRaw = false)
        => Ok(await Mediator.Send(new GetHistoryReplayQuery(vehicleId, from, to, routeMaxPoints, playbackMaxPoints, includeRaw)));

    [HttpPost("history/replay/insights")]
    [RequirePermission("GPS.View")]
    public async Task<IActionResult> PostHistoryReplayInsights([FromBody] PostHistoryReplayInsightsRequest body)
        => Ok(await Mediator.Send(new PostHistoryReplayInsightsCommand(
            body.VehicleId,
            body.FromDate,
            body.ToDate)));

    [HttpGet("history/{vehicleId:int}/export")]
    public async Task<IActionResult> ExportHistory(
        int vehicleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string format = "csv")
    {
        var result = await Mediator.Send(new GetHistoryExportQuery(vehicleId, from, to, format));
        if (!result.Success || result.Data is null)
            return Ok(result);
        return File(result.Data.Bytes, result.Data.ContentType, result.Data.FileName);
    }

    [HttpGet("trips/analytics")]
    public async Task<IActionResult> GetTripAnalytics([FromQuery] int? vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetTripAnalyticsQuery(vehicleId, from, to)));

    [HttpGet("trips/replay")]
    public async Task<IActionResult> GetTripReplay(
        [FromQuery] int? vehicleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? routeMaxPoints,
        [FromQuery] int? playbackMaxPoints,
        [FromQuery] bool includeRaw = false)
        => Ok(await Mediator.Send(new GetTripReplayQuery(vehicleId, from, to, routeMaxPoints, playbackMaxPoints, includeRaw)));

    [HttpGet("dashboard/fleet-status")]
    public async Task<IActionResult> GetFleetStatus()
        => Ok(await Mediator.Send(new GetGpsFleetStatusQuery()));

    [HttpGet("dashboard/fleet-status-local")]
    public async Task<IActionResult> GetFleetStatusLocal()
        => Ok(await Mediator.Send(new GetGpsFleetStatusLocalQuery()));

    [HttpGet("dashboard/operator-summary")]
    [RequirePermission("GPS.View")]
    public async Task<IActionResult> GetOperatorDashboard()
        => Ok(await Mediator.Send(new GetGpsOperatorDashboardQuery()));

    [HttpPost("operator/insights")]
    [RequirePermission("GPS.View")]
    public async Task<IActionResult> PostOperatorInsights([FromBody] PostGpsOperatorInsightsRequest body)
        => Ok(await Mediator.Send(new PostGpsOperatorInsightsCommand(body.QueryKey ?? body.Query ?? string.Empty)));

    [HttpGet("dashboard/fleet-status-history")]
    public async Task<IActionResult> GetFleetStatusHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetGpsFleetStatusHistoryQuery(from, to)));

    [HttpGet("trips/context")]
    public async Task<IActionResult> GetTripContext([FromQuery] int vehicleId)
        => Ok(await Mediator.Send(new GetTripContextQuery(vehicleId)));

    [HttpGet("trips")]
    public async Task<IActionResult> GetTrips(
        [FromQuery] int? vehicleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] double? minDistanceKm = null,
        [FromQuery] double? maxDistanceKm = null,
        [FromQuery] decimal? minAvgSpeedKmh = null,
        [FromQuery] decimal? maxAvgSpeedKmh = null,
        [FromQuery] string? status = null)
        => Ok(await Mediator.Send(new GetGpsTripsQuery(
            vehicleId, from, to, branchId, departmentId, driverId,
            page, pageSize, false, search, sortBy, sortDir,
            minDistanceKm, maxDistanceKm, minAvgSpeedKmh, maxAvgSpeedKmh, status)));

    [HttpGet("trips/{tripKey}")]
    public async Task<IActionResult> GetTripDetail(string tripKey)
        => Ok(await Mediator.Send(new GetTripDetailQuery(tripKey)));

    [HttpGet("trips/fleet-summary")]
    public async Task<IActionResult> GetFleetTripSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] int? departmentId,
        [FromQuery] int? driverId)
        => Ok(await Mediator.Send(new GetFleetTripSummaryQuery(from, to, branchId, departmentId, driverId)));

    [HttpGet("geofences")]
    public async Task<IActionResult> GetGeofences(
        [FromQuery] string? search,
        [FromQuery] string? areaType,
        [FromQuery] bool? isActive,
        [FromQuery] int? vehicleId)
        => Ok(await Mediator.Send(new GetGeofencesQuery(search, areaType, isActive, vehicleId)));

    [HttpGet("geofences/stats")]
    public async Task<IActionResult> GetGeofenceStats()
        => Ok(await Mediator.Send(new GetGeofenceStatsQuery()));

    [HttpPost("geofences")]
    public async Task<IActionResult> CreateGeofence([FromBody] CreateGeofenceDto geofence)
    {
        var result = await Mediator.Send(new CreateGeofenceCommand(geofence));
        return Created(string.Empty, result);
    }

    [HttpPut("geofences/{id:int}")]
    public async Task<IActionResult> UpdateGeofence(int id, [FromBody] UpdateGeofenceDto geofence)
        => Ok(await Mediator.Send(new UpdateGeofenceCommand(id, geofence)));

    [HttpDelete("geofences/{id:int}")]
    public async Task<IActionResult> DeleteGeofence(int id)
        => Ok(await Mediator.Send(new DeleteGeofenceCommand(id)));

    [HttpPost("geofences/{id:int}/duplicate")]
    public async Task<IActionResult> DuplicateGeofence(int id)
    {
        var result = await Mediator.Send(new DuplicateGeofenceCommand(id));
        return Created(string.Empty, result);
    }

    [HttpGet("geofences/{id:int}/assignments")]
    public async Task<IActionResult> GetGeofenceAssignments(int id)
        => Ok(await Mediator.Send(new GetGeofenceAssignmentsQuery(id)));

    [HttpPost("geofences/{id:int}/assignments")]
    public async Task<IActionResult> UpsertGeofenceAssignments(int id, [FromBody] UpsertGeofenceAssignmentsDto body)
        => Ok(await Mediator.Send(new UpsertGeofenceAssignmentsCommand(id, body)));

    [HttpDelete("geofences/{id:int}/assignments/{assignmentId:int}")]
    public async Task<IActionResult> DeleteGeofenceAssignment(int id, int assignmentId)
        => Ok(await Mediator.Send(new DeleteGeofenceAssignmentCommand(id, assignmentId)));

    [HttpGet("geofences/{id:int}/events")]
    public async Task<IActionResult> GetGeofenceEvents(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetGeofenceEventsQuery(id, from, to)));

    [HttpGet("alerts/rules")]
    public async Task<IActionResult> GetAlertRules()
        => Ok(await Mediator.Send(new GetGpsAlertRulesQuery()));

    [HttpPost("alerts/rules")]
    public async Task<IActionResult> CreateAlertRule([FromBody] CreateGpsAlertRuleDto rule)
    {
        var result = await Mediator.Send(new CreateGpsAlertRuleCommand(rule));
        return Created(string.Empty, result);
    }

    [HttpGet("alerts/events")]
    [RequirePermission(GpsPermissions.AlertView)]
    public async Task<IActionResult> GetAlertEvents(
        [FromQuery] int? vehicleId,
        [FromQuery] bool? unacknowledgedOnly,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? driverId,
        [FromQuery] string? eventType,
        [FromQuery] string? severity,
        [FromQuery] string? status,
        [FromQuery] string? readState,
        [FromQuery] string? datePreset,
        [FromQuery] int? geofenceId)
        => Ok(await Mediator.Send(new GetGpsAlertEventsQuery(
            vehicleId, unacknowledgedOnly, from, to, driverId, eventType, severity, status, readState, datePreset, geofenceId)));

    [HttpGet("alerts/events/{id:int}")]
    [RequirePermission(GpsPermissions.AlertView)]
    public async Task<IActionResult> GetAlertEvent(int id)
        => Ok(await Mediator.Send(new GetGpsAlertEventByIdQuery(id)));

    [HttpGet("alerts/stats")]
    [RequirePermission(GpsPermissions.AlertView)]
    public async Task<IActionResult> GetAlertStats()
        => Ok(await Mediator.Send(new GetGpsAlertStatsQuery()));

    [HttpPost("alerts/events/{id:int}/acknowledge")]
    [RequirePermission(GpsPermissions.AlertAcknowledge)]
    public async Task<IActionResult> AcknowledgeAlert(int id)
        => Ok(await Mediator.Send(new AcknowledgeGpsAlertCommand(id)));

    [HttpPost("alerts/events/{id:int}/read")]
    [RequirePermission(GpsPermissions.AlertView)]
    public async Task<IActionResult> MarkAlertRead(int id)
        => Ok(await Mediator.Send(new MarkGpsAlertReadCommand(id)));

    [HttpPost("alerts/events/{id:int}/resolve")]
    [RequirePermission(GpsPermissions.AlertResolve)]
    public async Task<IActionResult> ResolveAlert(int id, [FromBody] ResolveGpsAlertDto resolution)
        => Ok(await Mediator.Send(new ResolveGpsAlertCommand(id, resolution)));

    [HttpPost("alerts/events/{id:int}/archive")]
    [RequirePermission(GpsPermissions.AlertArchive)]
    public async Task<IActionResult> ArchiveAlert(int id, [FromBody] ArchiveGpsAlertDto? archive)
        => Ok(await Mediator.Send(new ArchiveGpsAlertCommand(id, archive ?? new ArchiveGpsAlertDto(null))));

    [HttpDelete("alerts/events/{id:int}")]
    [RequirePermission(GpsPermissions.AlertDelete)]
    public async Task<IActionResult> DeleteAlertEvent(int id)
        => Ok(await Mediator.Send(new DeleteGpsAlertEventCommand(id)));

    [HttpGet("alerts/settings")]
    public async Task<IActionResult> GetAlertSettings()
        => Ok(await Mediator.Send(new GetAlertSettingsQuery()));

    [HttpPut("alerts/settings")]
    public async Task<IActionResult> UpdateAlertSettings([FromBody] UpdateAlertSettingsDto settings)
        => Ok(await Mediator.Send(new UpdateAlertSettingsCommand(settings)));

    [HttpGet("alerts/geofence-breaches/count")]
    public async Task<IActionResult> GetGeofenceBreachCount()
        => Ok(await Mediator.Send(new GetGeofenceBreachCountQuery()));

    [HttpGet("devices")]
    [Obsolete("Use GET /api/gps/trackers")]
    public async Task<IActionResult> GetDevices()
        => Ok(await Mediator.Send(new GetTrackersQuery()));

    [HttpPost("devices")]
    [Obsolete("Use POST /api/gps/trackers/register")]
    public async Task<IActionResult> CreateDevice([FromBody] CreateGpsDeviceDto device)
    {
        var tracker = MapLegacyCreate(device);
        var result = await Mediator.Send(new RegisterTrackerCommand(tracker));
        return Created(string.Empty, result);
    }

    [HttpPut("devices/{id:int}")]
    [Obsolete("Use PUT /api/gps/trackers/{id}")]
    public async Task<IActionResult> UpdateDevice(int id, [FromBody] UpdateGpsDeviceDto device)
    {
        var tracker = MapLegacyUpdate(device);
        return Ok(await Mediator.Send(new UpdateTrackerCommand(id, tracker)));
    }

    [HttpDelete("devices/{id:int}")]
    [Obsolete("Use DELETE /api/gps/trackers/{id}")]
    public async Task<IActionResult> DeleteDevice(int id)
        => Ok(await Mediator.Send(new DeleteTrackerCommand(id)));

    // ── Tracker registration (SheikhGo master, Traccar engine) ─────────────

    [HttpGet("trackers")]
    public async Task<IActionResult> GetTrackers()
        => Ok(await Mediator.Send(new GetTrackersQuery()));

    [HttpGet("trackers/{id:int}")]
    public async Task<IActionResult> GetTracker(int id)
        => Ok(await Mediator.Send(new GetTrackerByIdQuery(id)));

    [HttpPost("trackers/register")]
    public async Task<IActionResult> RegisterTracker([FromBody] RegisterTrackerDto tracker)
    {
        var result = await Mediator.Send(new RegisterTrackerCommand(tracker));
        return result.Success ? Created(string.Empty, result) : BadRequest(result);
    }

    [HttpPut("trackers/{id:int}")]
    public async Task<IActionResult> UpdateTracker(int id, [FromBody] UpdateTrackerDto tracker)
        => Ok(await Mediator.Send(new UpdateTrackerCommand(id, tracker)));

    [HttpDelete("trackers/{id:int}")]
    public async Task<IActionResult> DeleteTracker(int id)
        => Ok(await Mediator.Send(new DeleteTrackerCommand(id)));

    [HttpPost("trackers/{id:int}/install")]
    public async Task<IActionResult> InstallTracker(int id, [FromBody] InstallTrackerDto body)
    {
        var result = await Mediator.Send(new InstallTrackerCommand(id, body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("trackers/{id:int}/uninstall")]
    public async Task<IActionResult> UninstallTracker(int id, [FromBody] UninstallTrackerDto? body = null)
    {
        var result = await Mediator.Send(new UninstallTrackerCommand(id, body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("trackers/{id:int}/transfer")]
    public async Task<IActionResult> TransferTracker(int id, [FromBody] TransferTrackerDto body)
    {
        var result = await Mediator.Send(new TransferTrackerCommand(id, body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("trackers/install-vehicles")]
    public async Task<IActionResult> GetTrackerInstallVehicles([FromQuery] int? trackerId)
        => Ok(await Mediator.Send(new GetTrackerInstallVehiclesQuery(trackerId)));

    [HttpGet("trackers/{id:int}/assignments")]
    public async Task<IActionResult> GetTrackerAssignments(int id)
        => Ok(await Mediator.Send(new GetTrackerAssignmentsQuery(id)));

    [HttpPost("trackers/{id:int}/sync")]
    public async Task<IActionResult> SyncTracker(int id)
        => Ok(await Mediator.Send(new SyncTrackerCommand(id)));

    [HttpPost("trackers/sync-all")]
    public async Task<IActionResult> SyncAllTrackers()
        => Ok(await Mediator.Send(new SyncAllTrackersCommand()));

    private static RegisterTrackerDto MapLegacyCreate(CreateGpsDeviceDto d) => new(
        d.Name,
        d.UniqueId,
        Category: "car",
        TrackerModelId: 0,
        TrackerModelKey: ResolveModelKey(d.Model),
        Phone: d.SimNumber,
        SupportsEngineCutoff: d.SupportsEngineCutoff,
        RelayOutput: d.RelayOutput,
        VehicleId: d.VehicleId,
        SerialNumber: d.SerialNumber,
        InstallationDate: d.InstallationDate,
        InstalledBy: d.InstalledBy,
        InstallationNotes: d.InstallationNotes,
        Vendor: d.Vendor);

    private static UpdateTrackerDto MapLegacyUpdate(UpdateGpsDeviceDto d) => new(
        d.Name,
        Category: "car",
        TrackerModelId: 0,
        TrackerModelKey: "teltonika_fmb920",
        SupportsEngineCutoff: d.SupportsEngineCutoff,
        RelayOutput: d.RelayOutput,
        VehicleId: d.VehicleId,
        SerialNumber: d.SerialNumber,
        InstallationDate: d.InstallationDate,
        InstalledBy: d.InstalledBy,
        InstallationNotes: d.InstallationNotes,
        IsActive: d.IsActive);

    private static string ResolveModelKey(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "teltonika_fmb920";
        var match = TrackerCatalog.Models.FirstOrDefault(m =>
            string.Equals(m.Value.Label, model, StringComparison.OrdinalIgnoreCase));
        return match.Key ?? "teltonika_fmb920";
    }

    [RequirePermission(GpsPermissions.CommandView)]
    [HttpGet("commands/supported/{deviceId:int}")]
    public async Task<IActionResult> GetSupportedCommands(int deviceId)
        => Ok(await Mediator.Send(new GetDeviceSupportedCommandsQuery(deviceId)));

    [RequirePermission(GpsPermissions.CommandView)]
    [HttpGet("commands/library")]
    public async Task<IActionResult> GetCommandLibrary()
        => Ok(await Mediator.Send(new GetGpsCommandDefinitionsQuery()));

    [RequirePermission(GpsPermissions.CommandView)]
    [HttpGet("commands/library/parameters")]
    public async Task<IActionResult> GetCommandLibraryParameters([FromQuery] string? commandKey = null)
        => Ok(await Mediator.Send(new GetGpsCommandParametersQuery(commandKey)));

    [RequirePermission(GpsPermissions.CommandSend)]
    [HttpPost("commands/send")]
    public async Task<IActionResult> SendCommand([FromBody] SendDeviceCommandDto command)
    {
        var result = await Mediator.Send(new SendDeviceCommandCommand(command));
        return Created(string.Empty, result);
    }

    [RequirePermission(GpsPermissions.CommandView)]
    [HttpGet("commands/{deviceId:int}")]
    public async Task<IActionResult> GetCommands(
        int deviceId,
        [FromQuery] string? status,
        [FromQuery] string? commandType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => Ok(await Mediator.Send(new GetDeviceCommandsQuery(deviceId, status, commandType, from, to, page, pageSize)));

    [RequirePermission(GpsPermissions.CommandView)]
    [HttpGet("commands/item/{id:int}")]
    public async Task<IActionResult> GetCommandById(int id)
        => Ok(await Mediator.Send(new GetDeviceCommandByIdQuery(id)));

    [RequirePermission(GpsPermissions.CommandView)]
    [HttpGet("commands/vehicle/{vehicleId:int}")]
    public async Task<IActionResult> GetVehicleCommands(int vehicleId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await Mediator.Send(new GetVehicleCommandsQuery(vehicleId, page, pageSize)));

    [RequirePermission(GpsPermissions.CommandRetry)]
    [HttpPost("commands/{id:int}/retry")]
    public async Task<IActionResult> RetryCommand(int id)
        => Ok(await Mediator.Send(new RetryDeviceCommandCommand(id)));

    [RequirePermission(GpsPermissions.CommandCancel)]
    [HttpPost("commands/{id:int}/cancel")]
    public async Task<IActionResult> CancelCommand(int id, [FromQuery] string? reason)
        => Ok(await Mediator.Send(new CancelDeviceCommandCommand(id, reason)));

    [HttpGet("commands/pending")]
    [AllowAnonymous]
    [GpsDeviceApiKey]
    public async Task<IActionResult> GetPendingCommands([FromQuery] string uniqueId)
        => Ok(await Mediator.Send(new GetPendingDeviceCommandsQuery(uniqueId)));

    [HttpPost("commands/{id:int}/complete")]
    [AllowAnonymous]
    [GpsDeviceApiKey]
    public async Task<IActionResult> CompleteCommand(int id, [FromBody] CompleteDeviceCommandDto body)
        => Ok(await Mediator.Send(new CompleteDeviceCommandCommand(id, body.UniqueId, body.Status, body.ResponseText, body.ErrorMessage)));

    [HttpGet("eta")]
    public async Task<IActionResult> GetEta([FromQuery] int bookingId)
        => Ok(await Mediator.Send(new GetGpsEtaQuery(bookingId)));

    // ── Traccar admin endpoints ────────────────────────────────────────────

    [HttpGet("traccar/status")]
    [RequirePermission(AnalyticsPermissions.GpsView)]
    public async Task<IActionResult> GetTraccarStatus(
        [FromServices] ITraccarClient traccar,
        [FromServices] IDbConnectionFactory dbFactory,
        [FromServices] ITenantContext tenantContext,
        [FromServices] IOptions<TraccarOptions> traccarOptions)
    {
        var syncEnabled = traccarOptions.Value.Enabled;
        var server = await traccar.GetServerAsync(HttpContext.RequestAborted);
        var devices = await traccar.GetDevicesAsync(HttpContext.RequestAborted);
        var connected = server is not null || devices.Count > 0;

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();
        var linkedCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            SELECT COUNT(*)
            FROM GpsDevices d
            LEFT JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
            WHERE d.IsDeleted = 0
              AND d.TraccarDeviceId IS NOT NULL
              {TrackerTenantSql.DeviceScopeFilter}
            """,
            new { TenantId = tenantId },
            cancellationToken: HttpContext.RequestAborted));

        if (!syncEnabled)
        {
            var reachabilityNote = connected
                ? "Traccar reachable but sync is disabled (Traccar:Enabled=false)."
                : "Traccar sync is disabled (Traccar:Enabled=false).";
            return Ok(new TraccarStatusDto(
                Connected: connected,
                ServerVersion: server?.Version,
                DeviceCount: linkedCount,
                LastError: reachabilityNote,
                SyncEnabled: false));
        }

        if (server is not null)
            return Ok(new TraccarStatusDto(true, server.Version, linkedCount, SyncEnabled: true));

        if (connected)
            return Ok(new TraccarStatusDto(true, null, linkedCount, "Server info unavailable; device API reachable.", SyncEnabled: true));

        return Ok(new TraccarStatusDto(false, null, linkedCount, "Traccar server unreachable.", SyncEnabled: true));
    }

    [HttpGet("traccar/devices")]
    public async Task<IActionResult> GetTraccarDevices(
        [FromServices] ITraccarClient traccar)
    {
        var devices = await traccar.GetDevicesAsync(HttpContext.RequestAborted);
        return Ok(devices);
    }

    [HttpPost("traccar/sync")]
    public async Task<IActionResult> RunTraccarSync(
        [FromServices] ITraccarSyncOrchestrator orchestrator)
        => Ok(await orchestrator.RunManualSyncAsync(HttpContext.RequestAborted));

    [HttpGet("traccar/sync-status")]
    public async Task<IActionResult> GetTraccarSyncStatus(
        [FromServices] ITraccarSyncState syncState,
        [FromServices] ITraccarClient traccar,
        [FromServices] IOptions<TraccarOptions> traccarOptions)
    {
        if (!traccarOptions.Value.Enabled)
            return Ok(syncState.Snapshot(connected: false));

        var server = await traccar.GetServerAsync(HttpContext.RequestAborted);
        var devices = await traccar.GetDevicesAsync(HttpContext.RequestAborted);
        var connected = server is not null || devices.Count > 0;
        return Ok(syncState.Snapshot(connected));
    }

    /// <summary>Deprecated — use POST traccar/sync for full manual sync.</summary>
    [HttpPost("traccar/sync-devices")]
    public async Task<IActionResult> SyncTraccarDevices(
        [FromServices] ITraccarSyncOrchestrator orchestrator)
    {
        var result = await orchestrator.SyncDevicesAsync(HttpContext.RequestAborted);
        var job = result.Jobs.FirstOrDefault(j => j.Job == "devices");
        return Ok(new TraccarSyncResultDto(job?.Imported ?? 0, job?.Updated ?? 0, job?.Skipped ?? 0));
    }
}

public sealed class PostGpsOperatorInsightsRequest
{
    public string? QueryKey { get; set; }
    public string? Query { get; set; }
}

public sealed class PostHistoryReplayInsightsRequest
{
    public int VehicleId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Deprecated aliases — use /api/gps/* instead.
/// </summary>
[Authorize]
[RequirePermission(AnalyticsPermissions.GpsView)]
[Route("api/tracking")]
public class TrackingController : BaseApiController
{
    [HttpPost("location")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationCommand command)
    {
        var dto = command.Location;
        return Ok(await Mediator.Send(new IngestPositionCommand(new IngestPositionDto(
            dto.VehicleId, dto.DriverId, dto.BookingId, null,
            dto.Latitude, dto.Longitude, dto.Speed))));
    }

    [HttpGet("live")]
    public async Task<IActionResult> GetLive()
        => Ok(await Mediator.Send(new GetLiveTrackingQuery()));

    [HttpGet("history/{vehicleId}")]
    public async Task<IActionResult> GetHistory(int vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetTrackingHistoryQuery(vehicleId, from, to)));
}
