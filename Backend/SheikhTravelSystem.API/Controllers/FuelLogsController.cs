using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.FuelLogs.Commands;
using SheikhTravelSystem.Application.Features.FuelLogs.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class FuelLogsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetFuelLogsQuery query)
        => Ok(await Mediator.Send(query));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFuelLogCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }
}
