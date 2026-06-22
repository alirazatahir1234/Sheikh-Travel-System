using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
public class VendorsController : BaseApiController
{
    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new ListVendorsQuery()));

    [RequirePermission(MaintenancePermissions.View)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetVendorByIdQuery(id)));

    [RequirePermission(MaintenancePermissions.VendorManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVendorDto body)
        => Ok(await Mediator.Send(new CreateVendorCommand(body)));

    [RequirePermission(MaintenancePermissions.VendorManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVendorDto body)
        => Ok(await Mediator.Send(new UpdateVendorCommand(id, body)));

    [RequirePermission(MaintenancePermissions.VendorManage)]
    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
        => Ok(await Mediator.Send(new SetVendorActiveCommand(id, true)));

    [RequirePermission(MaintenancePermissions.VendorManage)]
    [HttpPut("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
        => Ok(await Mediator.Send(new SetVendorActiveCommand(id, false)));
}
