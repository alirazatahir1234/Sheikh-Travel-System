using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.FuelLogs.Commands;
using SheikhTravelSystem.Application.Features.FuelLogs.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages fuel log operations.
/// </summary>
public class FuelLogsController : BaseApiController
{
    /// <summary>
    /// Gets fuel logs using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetFuelLogsQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Creates a new fuel log entry.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFuelLogCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }
}
