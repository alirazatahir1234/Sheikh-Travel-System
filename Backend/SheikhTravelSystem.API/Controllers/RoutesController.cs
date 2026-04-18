using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Routes.Commands;
using SheikhTravelSystem.Application.Features.Routes.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class RoutesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetRoutesQuery query)
        => Ok(await Mediator.Send(query));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRouteCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRouteCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
