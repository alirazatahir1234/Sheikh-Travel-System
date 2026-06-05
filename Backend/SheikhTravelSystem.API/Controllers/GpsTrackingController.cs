using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
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
    public async Task<IActionResult> GetDevices()
        => Ok(await Mediator.Send(new GetGpsDevicesQuery()));

    [HttpPost("devices")]
    public async Task<IActionResult> CreateDevice([FromBody] CreateGpsDeviceDto device)
    {
        var result = await Mediator.Send(new CreateGpsDeviceCommand(device));
        return Created(string.Empty, result);
    }

    [HttpPut("devices/{id:int}")]
    public async Task<IActionResult> UpdateDevice(int id, [FromBody] UpdateGpsDeviceDto device)
        => Ok(await Mediator.Send(new UpdateGpsDeviceCommand(id, device)));

    [HttpDelete("devices/{id:int}")]
    public async Task<IActionResult> DeleteDevice(int id)
        => Ok(await Mediator.Send(new DeleteGpsDeviceCommand(id)));

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
