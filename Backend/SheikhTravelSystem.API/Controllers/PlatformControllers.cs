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

[RequirePermission(PlatformPermissions.TenantsManage)]
[ApiController]
[Route("api/platform/tenants/{tenantId:int}")]
public class TenantOrganizationController : BaseApiController
{
    [HttpGet("organization")]
    public async Task<IActionResult> GetOrganizationTree(int tenantId)
        => Ok(await Mediator.Send(new GetOrganizationTreeQuery(tenantId)));

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(int tenantId)
        => Ok(await Mediator.Send(new GetBranchesForTenantQuery(tenantId)));

    [HttpPost("branches")]
    public async Task<IActionResult> CreateBranch(int tenantId, [FromBody] BranchUpsertPayload payload)
        => Ok(await Mediator.Send(new CreateBranchForTenantCommand(tenantId, payload)));

    [HttpPut("branches/{branchId:int}")]
    public async Task<IActionResult> UpdateBranch(int tenantId, int branchId, [FromBody] BranchUpsertPayload payload)
        => Ok(await Mediator.Send(new UpdateBranchForTenantCommand(tenantId, branchId, payload)));

    [HttpDelete("branches/{branchId:int}")]
    public async Task<IActionResult> DeleteBranch(int tenantId, int branchId)
        => Ok(await Mediator.Send(new DeleteBranchForTenantCommand(tenantId, branchId)));

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments(int tenantId)
        => Ok(await Mediator.Send(new GetDepartmentsForTenantQuery(tenantId)));

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(int tenantId, [FromBody] DepartmentUpsertWithBranchPayload payload)
        => Ok(await Mediator.Send(new CreateDepartmentForTenantCommand(tenantId, payload)));

    [HttpPut("departments/{departmentId:int}")]
    public async Task<IActionResult> UpdateDepartment(int tenantId, int departmentId, [FromBody] UpdateDepartmentWithBranchRequest request)
        => Ok(await Mediator.Send(new UpdateDepartmentForTenantCommand(tenantId, departmentId, request.Payload, request.IsActive)));

    [HttpDelete("departments/{departmentId:int}")]
    public async Task<IActionResult> DeleteDepartment(int tenantId, int departmentId)
        => Ok(await Mediator.Send(new DeleteDepartmentForTenantCommand(tenantId, departmentId)));

    [HttpPost("departments/{departmentId:int}/move")]
    public async Task<IActionResult> MoveDepartment(int tenantId, int departmentId, [FromBody] MoveDepartmentRequest request)
        => Ok(await Mediator.Send(new MoveDepartmentCommand(tenantId, departmentId, request.NewBranchId)));
}

public record UpdateDepartmentWithBranchRequest(DepartmentUpsertWithBranchPayload Payload, bool IsActive);
public record MoveDepartmentRequest(int? NewBranchId);

[RequirePermission(PlatformPermissions.RolesView)]
[ApiController]
[Route("api/platform/tenants/{tenantId:int}")]
public class TenantAccessController : BaseApiController
{
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(int tenantId)
        => Ok(await Mediator.Send(new GetRolesForTenantQuery(tenantId)));

    [RequirePermission(PlatformPermissions.RolesManage)]
    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(int tenantId, [FromBody] CreateRoleRequest request)
        => Ok(await Mediator.Send(new CreateRoleForTenantCommand(tenantId, request.Name, request.Code)));

    [RequirePermission(PlatformPermissions.RolesManage)]
    [HttpPut("roles/{roleId:int}")]
    public async Task<IActionResult> UpdateRole(int tenantId, int roleId, [FromBody] UpdateRoleForTenantRequest request)
        => Ok(await Mediator.Send(new UpdateRoleForTenantCommand(tenantId, roleId, request.Name, request.IsActive)));

    [RequirePermission(PlatformPermissions.RolesManage)]
    [HttpDelete("roles/{roleId:int}")]
    public async Task<IActionResult> DeleteRole(int tenantId, int roleId)
        => Ok(await Mediator.Send(new DeleteRoleForTenantCommand(tenantId, roleId)));

    [RequirePermission(PlatformPermissions.RolesManage)]
    [HttpPut("roles/{roleId:int}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(int tenantId, int roleId, [FromBody] UpdateRolePermissionsRequest request)
        => Ok(await Mediator.Send(new UpdateRolePermissionsForTenantCommand(tenantId, roleId, request.PermissionCodes)));

    [HttpGet("security")]
    public async Task<IActionResult> GetSecuritySettings(int tenantId)
        => Ok(await Mediator.Send(new GetTenantSecuritySettingsQuery(tenantId)));

    [RequirePermission(PlatformPermissions.TenantsManage)]
    [HttpPut("security")]
    public async Task<IActionResult> UpdateSecuritySettings(int tenantId, [FromBody] TenantSecuritySettingsDto payload)
        => Ok(await Mediator.Send(new UpdateTenantSecuritySettingsCommand(tenantId, payload)));

    [RequirePermission(PlatformPermissions.RolesManage)]
    [HttpPost("roles/apply-template")]
    public async Task<IActionResult> ApplyRoleTemplate(int tenantId, [FromBody] ApplyRoleTemplateRequest request)
        => Ok(await Mediator.Send(new ApplyRoleTemplateCommand(tenantId, request.RoleCode)));
}

[RequirePermission(PlatformPermissions.RolesView)]
[ApiController]
[Route("api/platform/role-templates")]
public class RoleTemplatesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetRoleTemplatesQuery()));
}

public record CreateRoleRequest(string Name, string Code);
public record UpdateRoleForTenantRequest(string Name, bool IsActive);
public record UpdateRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);
public record ApplyRoleTemplateRequest(string RoleCode);

[RequirePermission(PlatformPermissions.TenantsManage)]
[ApiController]
[Route("api/platform/tenants/{tenantId:int}")]
public class TenantModuleManagementController : BaseApiController
{
    [HttpGet("module-overview")]
    public async Task<IActionResult> GetModuleOverview(int tenantId)
        => Ok(await Mediator.Send(new GetTenantModuleOverviewQuery(tenantId)));

    [HttpPut("modules")]
    public async Task<IActionResult> SetModules(int tenantId, [FromBody] SetTenantModulesRequest request)
        => Ok(await Mediator.Send(new SetTenantModulesCommand(tenantId, request.ModuleCodes)));
}

public record SetTenantModulesRequest(IReadOnlyList<string> ModuleCodes);

[RequirePermission(PlatformPermissions.TenantsManage)]
[ApiController]
[Route("api/platform/tenants/{tenantId:int}")]
public class TenantSubscriptionController : BaseApiController
{
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription(int tenantId)
        => Ok(await Mediator.Send(new GetSubscriptionOverviewQuery(tenantId)));

    [HttpPost("subscription/action")]
    public async Task<IActionResult> UpdateSubscription(int tenantId, [FromBody] UpdateSubscriptionRequest request)
    {
        if (!Enum.TryParse<SubscriptionAction>(request.Action, ignoreCase: true, out var action))
            return BadRequest(ApiResponse<bool>.FailResponse($"Unknown action '{request.Action}'."));

        return Ok(await Mediator.Send(new UpdateSubscriptionCommand(
            tenantId, action, request.PlanName, request.MonthlyAmount, request.AutoRenew, request.BillingCycle)));
    }
}

public record UpdateSubscriptionRequest(
    string Action,
    string? PlanName,
    decimal? MonthlyAmount,
    bool? AutoRenew,
    string? BillingCycle);
