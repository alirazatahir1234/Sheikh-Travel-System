using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Tracking.Commands;
using SheikhTravelSystem.Application.Features.Tracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Provides live and historical tracking endpoints.
/// </summary>
public class TrackingController : BaseApiController
{
    /// <summary>
    /// Updates current location for a tracked entity.
    /// </summary>
    [HttpPost("location")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>
    /// Gets live tracking information.
    /// </summary>
    [HttpGet("live")]
    public async Task<IActionResult> GetLive()
        => Ok(await Mediator.Send(new GetLiveTrackingQuery()));

    /// <summary>
    /// Gets tracking history for a vehicle and date range.
    /// </summary>
    [HttpGet("history/{vehicleId}")]
    public async Task<IActionResult> GetHistory(int vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetTrackingHistoryQuery(vehicleId, from, to)));
}
