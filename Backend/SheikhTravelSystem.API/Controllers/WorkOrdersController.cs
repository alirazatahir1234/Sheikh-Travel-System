using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class WorkOrdersController : BaseApiController
{
    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await Mediator.Send(new GetWorkOrderStatsQuery()));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("technicians")]
    public async Task<IActionResult> GetTechnicians([FromQuery] int? workshopId)
        => Ok(await Mediator.Send(new ListTechniciansQuery(workshopId)));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ListWorkOrdersQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetWorkOrderByIdQuery(id)));

    [RequirePermission(MaintenancePermissions.WorkOrderManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkOrderDto body)
        => Ok(await Mediator.Send(new CreateWorkOrderCommand(body)));

    [RequirePermission(MaintenancePermissions.WorkOrderManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWorkOrderDto body)
        => Ok(await Mediator.Send(new UpdateWorkOrderCommand(id, body)));

    [RequirePermission(MaintenancePermissions.WorkOrderManage)]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateWorkOrderStatusDto body)
        => Ok(await Mediator.Send(new UpdateWorkOrderStatusCommand(id, body)));

    [RequirePermission(MaintenancePermissions.WorkOrderManage)]
    [HttpPost("{id:int}/parts")]
    public async Task<IActionResult> RecordPartUsage(int id, [FromBody] RecordPartUsageRequest body)
        => Ok(await Mediator.Send(new RecordPartUsageCommand(id, body.PartId, body.Quantity)));
}

public record RecordPartUsageRequest(int PartId, int Quantity);
