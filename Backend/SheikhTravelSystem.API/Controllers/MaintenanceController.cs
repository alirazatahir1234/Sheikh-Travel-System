using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Features.Maintenance.Commands;
using SheikhTravelSystem.Application.Features.Maintenance.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class MaintenanceController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetMaintenanceQuery query)
        => Ok(await Mediator.Send(query));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaintenanceCommand command)
    {
        var result = await Mediator.Send(command);
        return Created(string.Empty, result);
    }
}
