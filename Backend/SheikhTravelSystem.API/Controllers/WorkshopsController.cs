using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class WorkshopsController : BaseApiController
{
    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new ListWorkshopsQuery()));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetWorkshopByIdQuery(id)));

    [RequirePermission(MaintenancePermissions.WorkshopManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkshopDto body)
        => Ok(await Mediator.Send(new CreateWorkshopCommand(body)));

    [RequirePermission(MaintenancePermissions.WorkshopManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWorkshopDto body)
        => Ok(await Mediator.Send(new UpdateWorkshopCommand(id, body)));

    [RequirePermission(MaintenancePermissions.WorkshopManage)]
    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
        => Ok(await Mediator.Send(new SetWorkshopActiveCommand(id, true)));

    [RequirePermission(MaintenancePermissions.WorkshopManage)]
    [HttpPut("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
        => Ok(await Mediator.Send(new SetWorkshopActiveCommand(id, false)));
}
