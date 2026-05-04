using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Maintenance.Commands;
using SheikhTravelSystem.Application.Features.Maintenance.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages vehicle maintenance operations.
/// </summary>
public class MaintenanceController : BaseApiController
{
    /// <summary>
    /// Gets maintenance records using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetMaintenanceQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Gets a maintenance record by identifier.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetMaintenanceByIdQuery(id)));

    /// <summary>
    /// Creates a new maintenance record.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaintenanceCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Updates an existing maintenance record.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMaintenanceCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Updates the status of a maintenance record.
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateMaintenanceStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Deletes a maintenance record.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteMaintenanceCommand(id)));
}
