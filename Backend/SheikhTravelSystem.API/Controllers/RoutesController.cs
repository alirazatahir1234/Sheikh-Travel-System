using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Routes.Commands;
using SheikhTravelSystem.Application.Features.Routes.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
/// <summary>
/// Manages route operations.
/// </summary>
public class RoutesController : BaseApiController
{
    /// <summary>
    /// Gets routes using filter and pagination criteria.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetRoutesQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>
    /// Gets a single route by identifier.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetRouteByIdQuery(id)));

    /// <summary>
    /// Creates a new route.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRouteCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Updates an existing route by identifier.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRouteCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>
    /// Soft-deletes a route by identifier.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteRouteCommand(id)));
}
