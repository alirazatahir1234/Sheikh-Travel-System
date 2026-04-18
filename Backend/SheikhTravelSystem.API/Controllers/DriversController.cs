using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Drivers.Commands;
using SheikhTravelSystem.Application.Features.Drivers.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class DriversController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetDriversQuery query)
        => Ok(await Mediator.Send(query));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDriverCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
