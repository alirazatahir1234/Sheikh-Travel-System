using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;
using SheikhTravelSystem.Application.Features.Vehicles.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages vehicle operations.
/// </summary>
public class VehiclesController : BaseApiController
{
    /// <summary>
    /// Gets vehicles using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetVehiclesQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Gets vehicle details by identifier.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetVehicleByIdQuery(id)));

    /// <summary>
    /// Creates a new vehicle.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVehicleCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, result);
    }

    /// <summary>
    /// Updates an existing vehicle by identifier.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVehicleCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Deletes a vehicle by identifier.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteVehicleCommand(id)));

    /// <summary>
    /// Toggles vehicle status between Available and Retired.
    /// </summary>
    [HttpPatch("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
        => Ok(await Mediator.Send(new ToggleVehicleStatusCommand(id)));
}
