using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Tracking.Commands;
using SheikhTravelSystem.Application.Features.Tracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class TrackingController : BaseApiController
{
    [HttpPost("location")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationCommand command)
        => Ok(await Mediator.Send(command));

    [HttpGet("live")]
    public async Task<IActionResult> GetLive()
        => Ok(await Mediator.Send(new GetLiveTrackingQuery()));

    [HttpGet("history/{vehicleId}")]
    public async Task<IActionResult> GetHistory(int vehicleId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await Mediator.Send(new GetTrackingHistoryQuery(vehicleId, from, to)));
}
