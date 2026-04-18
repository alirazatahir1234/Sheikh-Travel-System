using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Drivers.Commands;
using SheikhTravelSystem.Application.Features.Drivers.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages driver operations.
/// </summary>
public class DriversController : BaseApiController
{
    /// <summary>
    /// Gets drivers using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetDriversQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Creates a new driver.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Updates an existing driver by identifier.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDriverCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
