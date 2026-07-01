using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.Tracking.Commands;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;
using SheikhTravelSystem.Application.Features.Tracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize(Roles = "Admin,Dispatcher,Accountant,Driver")]
/// <summary>
/// GPS fleet tracking — live positions, history, trips, geofences, alerts, devices, and commands.
/// </summary>
[Route("api/gps")]
public class GpsTrackingController : BaseApiController
{
    [HttpPost("positions")]
    [Authorize(Roles = "Admin,Dispatcher,Driver")]
    public async Task<IActionResult> IngestPosition([FromBody] IngestPositionDto position)
        => Ok(await Mediator.Send(new IngestPositionCommand(position)));

    [HttpGet("live")]
    public async Task<IActionResult> GetLive()
        => Ok(await Mediator.Send(new GetLivePositionsQuery()));

    [HttpGet("history/{vehicleId:int}")]
    public async Task<IActionResult> GetHistory(int vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetPositionHistoryQuery(vehicleId, from, to)));

    [HttpGet("trips")]
    public async Task<IActionResult> GetTrips([FromQuery] int? vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetGpsTripsQuery(vehicleId, from, to)));

    [HttpGet("geofences")]
    public async Task<IActionResult> GetGeofences()
        => Ok(await Mediator.Send(new GetGeofencesQuery()));

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
    public async Task<IActionResult> GetAlertEvents([FromQuery] int? vehicleId, [FromQuery] bool? unacknowledgedOnly)
        => Ok(await Mediator.Send(new GetGpsAlertEventsQuery(vehicleId, unacknowledgedOnly)));

    [HttpPost("alerts/events/{id:int}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(int id)
        => Ok(await Mediator.Send(new AcknowledgeGpsAlertCommand(id)));

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
    [Authorize(Roles = "Admin,Dispatcher,Accountant")]
    public async Task<IActionResult> GetTrackers()
        => Ok(await Mediator.Send(new GetTrackersQuery()));

    [HttpGet("trackers/{id:int}")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> GetTracker(int id)
        => Ok(await Mediator.Send(new GetTrackerByIdQuery(id)));

    [HttpPost("trackers/register")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> RegisterTracker([FromBody] RegisterTrackerDto tracker)
    {
        var result = await Mediator.Send(new RegisterTrackerCommand(tracker));
        return result.Success ? Created(string.Empty, result) : BadRequest(result);
    }

    [HttpPut("trackers/{id:int}")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> UpdateTracker(int id, [FromBody] UpdateTrackerDto tracker)
        => Ok(await Mediator.Send(new UpdateTrackerCommand(id, tracker)));

    [HttpDelete("trackers/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTracker(int id)
        => Ok(await Mediator.Send(new DeleteTrackerCommand(id)));

    [HttpPost("trackers/{id:int}/install")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> InstallTracker(int id, [FromBody] InstallTrackerDto body)
    {
        var result = await Mediator.Send(new InstallTrackerCommand(id, body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("trackers/{id:int}/uninstall")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> UninstallTracker(int id)
    {
        var result = await Mediator.Send(new UninstallTrackerCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("trackers/{id:int}/sync")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncTracker(int id)
        => Ok(await Mediator.Send(new SyncTrackerCommand(id)));

    [HttpPost("trackers/sync-all")]
    [Authorize(Roles = "Admin")]
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

    [RequirePermission(GpsPermissions.CommandSend)]
    [HttpPost("commands/send")]
    public async Task<IActionResult> SendCommand([FromBody] SendDeviceCommandDto command)
    {
        var result = await Mediator.Send(new SendDeviceCommandCommand(command));
        return Created(string.Empty, result);
    }

    [HttpGet("commands/{deviceId:int}")]
    public async Task<IActionResult> GetCommands(int deviceId)
        => Ok(await Mediator.Send(new GetDeviceCommandsQuery(deviceId)));

    [HttpGet("commands/pending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPendingCommands([FromQuery] string uniqueId)
        => Ok(await Mediator.Send(new GetPendingDeviceCommandsQuery(uniqueId)));

    [HttpPost("commands/{id:int}/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteCommand(int id, [FromQuery] string status = "ack")
        => Ok(await Mediator.Send(new CompleteDeviceCommandCommand(id, status)));

    [HttpGet("eta")]
    public async Task<IActionResult> GetEta([FromQuery] int bookingId)
        => Ok(await Mediator.Send(new GetGpsEtaQuery(bookingId)));

    // ── Traccar admin endpoints ────────────────────────────────────────────

    [HttpGet("traccar/status")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> GetTraccarStatus(
        [FromServices] ITraccarClient traccar)
    {
        var server = await traccar.GetServerAsync(HttpContext.RequestAborted);
        var devices = await traccar.GetDevicesAsync(HttpContext.RequestAborted);

        if (server is not null)
            return Ok(new TraccarStatusDto(true, server.Version, devices.Count));

        if (devices.Count > 0)
            return Ok(new TraccarStatusDto(true, null, devices.Count, "Server info unavailable; device API reachable."));

        return Ok(new TraccarStatusDto(false, null, 0, "Traccar server unreachable."));
    }

    [HttpGet("traccar/devices")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetTraccarDevices(
        [FromServices] ITraccarClient traccar)
    {
        var devices = await traccar.GetDevicesAsync(HttpContext.RequestAborted);
        return Ok(devices);
    }

    [HttpPost("traccar/sync")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RunTraccarSync(
        [FromServices] ITraccarSyncOrchestrator orchestrator)
        => Ok(await orchestrator.RunManualSyncAsync(HttpContext.RequestAborted));

    [HttpGet("traccar/sync-status")]
    [Authorize(Roles = "Admin,Dispatcher")]
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncTraccarDevices(
        [FromServices] ITraccarSyncOrchestrator orchestrator)
    {
        var result = await orchestrator.SyncDevicesAsync(HttpContext.RequestAborted);
        var job = result.Jobs.FirstOrDefault(j => j.Job == "devices");
        return Ok(new TraccarSyncResultDto(job?.Imported ?? 0, job?.Updated ?? 0, job?.Skipped ?? 0));
    }
}

/// <summary>
/// Deprecated aliases — use /api/gps/* instead.
/// </summary>
[Authorize]
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
