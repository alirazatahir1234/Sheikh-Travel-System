using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.API.Controllers;

[RequirePermission(PlatformPermissions.BranchesManage)]
[ApiController]
[Route("api/platform/branches")]
public class BranchesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetBranchesQuery()));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await Mediator.Send(new GetBranchByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BranchUpsertPayload payload)
        => Ok(await Mediator.Send(new CreateBranchCommand(payload)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BranchUpsertPayload payload)
        => Ok(await Mediator.Send(new UpdateBranchCommand(id, payload)));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteBranchCommand(id)));
}

[RequirePermission(PlatformPermissions.DepartmentsManage)]
[ApiController]
[Route("api/platform/departments")]
public class DepartmentsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetDepartmentsQuery()));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DepartmentUpsertPayload payload)
        => Ok(await Mediator.Send(new CreateDepartmentCommand(payload)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDepartmentRequest request)
        => Ok(await Mediator.Send(new UpdateDepartmentCommand(id, request.Payload, request.IsActive)));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => Ok(await Mediator.Send(new DeleteDepartmentCommand(id)));
}

[RequirePermission(PlatformPermissions.RolesView)]
[ApiController]
[Route("api/platform/roles")]
public class RolesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetRolesQuery()));

    [RequirePermission(PlatformPermissions.RolesManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleCommand command)
        => Ok(await Mediator.Send(command));

    [RequirePermission(PlatformPermissions.RolesManage)]
    [HttpPut("{id:int}/permissions")]
    public async Task<IActionResult> UpdatePermissions(int id, [FromBody] UpdateRolePermissionsCommand command)
        => Ok(await Mediator.Send(command with { RoleId = id }));
}

[RequirePermission(PlatformPermissions.RolesView)]
[ApiController]
[Route("api/platform/permissions")]
public class PermissionsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetPermissionsQuery()));
}

[RequirePermission(PlatformPermissions.TenantsManage)]
[ApiController]
[Route("api/platform/modules")]
public class TenantModulesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetTenantModulesQuery()));
}

[Authorize]
[ApiController]
[Route("api/platform/menus")]
public class PlatformMenusController : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyMenu()
        => Ok(await Mediator.Send(new GetUserMenuQuery()));
}
