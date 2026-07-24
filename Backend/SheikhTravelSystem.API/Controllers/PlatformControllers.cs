using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
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

    [HttpGet("company")]
    public async Task<IActionResult> GetCompanyRoles([FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetCompanyRolesQuery(tenantId)));

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
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category = null,
        [FromQuery] string? moduleKey = null,
        [FromQuery] string? action = null,
        [FromQuery] bool? visible = null)
        => Ok(await Mediator.Send(new GetPermissionsQuery(category, moduleKey, action, visible)));

    [HttpGet("effective")]
    [Authorize]
    public async Task<IActionResult> GetEffective()
        => Ok(await Mediator.Send(new GetEffectivePermissionsQuery()));
}

[RequirePermission(PlatformPermissions.TenantsManage)]
[ApiController]
[Route("api/platform/modules")]
public class TenantModulesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetTenantModulesQuery()));

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog()
        => Ok(await Mediator.Send(new GetModuleCatalogQuery()));

    [HttpGet("company")]
    public async Task<IActionResult> GetCompanyModules()
        => Ok(await Mediator.Send(new GetCompanyModulesQuery()));

    [HttpGet("{codeOrId}")]
    public async Task<IActionResult> GetByKey(string codeOrId)
        => Ok(await Mediator.Send(new GetModuleByKeyQuery(codeOrId)));
}

[Authorize]
[ApiController]
[Route("api/platform/menus")]
public class PlatformMenusController : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyMenu()
        => Ok(await Mediator.Send(new GetUserMenuQuery()));

    [HttpGet]
    [RequirePermission(PlatformPermissions.MenusManage)]
    public async Task<IActionResult> GetCatalog()
        => Ok(await Mediator.Send(new GetMenuCatalogQuery()));

    [HttpGet("catalog")]
    [Authorize]
    public async Task<IActionResult> GetCatalogAlias()
        => Ok(await Mediator.Send(new GetMenuCatalogQuery()));

    [HttpPut("modules/{id:int}")]
    [RequirePermission(PlatformPermissions.MenusManage)]
    public async Task<IActionResult> UpdateModule(int id, [FromBody] UpdateMenuModulePayload payload)
        => Ok(await Mediator.Send(new UpdateMenuModuleCommand(id, payload)));

    [HttpPut("{id:int}")]
    [RequirePermission(PlatformPermissions.MenusManage)]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] UpdateMenuItemPayload payload)
        => Ok(await Mediator.Send(new UpdateMenuItemCommand(id, payload)));

    [HttpPost]
    [RequirePermission(PlatformPermissions.MenusManage)]
    public async Task<IActionResult> CreateItem([FromBody] CreateMenuItemPayload payload)
        => Ok(await Mediator.Send(new CreateMenuItemCommand(payload)));

    [HttpDelete("{id:int}")]
    [RequirePermission(PlatformPermissions.MenusManage)]
    public async Task<IActionResult> DeleteItem(int id)
        => Ok(await Mediator.Send(new DeleteMenuItemCommand(id)));
}

[Authorize]
[ApiController]
[Route("api/platform/workspaces")]
public class PlatformWorkspacesController : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
        => Ok(await Mediator.Send(new GetMyWorkspaceQuery()));

    [HttpGet]
    [RequirePermission(PlatformPermissions.WorkspacesManage)]
    public async Task<IActionResult> GetCatalog()
        => Ok(await Mediator.Send(new GetWorkspaceCatalogQuery()));

    [HttpGet("catalog")]
    [Authorize]
    public async Task<IActionResult> GetCatalogAlias()
        => Ok(await Mediator.Send(new GetWorkspaceCatalogQuery()));

    [HttpGet("company")]
    [RequirePermission(PlatformPermissions.WorkspacesManage)]
    public async Task<IActionResult> GetCompany([FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetCompanyWorkspacesQuery(tenantId)));

    [HttpPut("company")]
    [RequirePermission(PlatformPermissions.WorkspacesManage)]
    public async Task<IActionResult> SetCompany([FromBody] SetCompanyWorkspacesRequest request)
        => Ok(await Mediator.Send(new SetCompanyWorkspacesCommand(
            request.TenantId,
            request.EnabledWorkspaceKeys ?? Array.Empty<string>())));

    [HttpPut("{key}")]
    [RequirePermission(PlatformPermissions.WorkspacesManage)]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateWorkspaceDefinitionPayload payload)
        => Ok(await Mediator.Send(new UpdateWorkspaceDefinitionCommand(key, payload)));

    [HttpPost]
    [RequirePermission(PlatformPermissions.WorkspacesManage)]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceDefinitionPayload payload)
        => Ok(await Mediator.Send(new CreateWorkspaceDefinitionCommand(payload)));

    [HttpDelete("{key}")]
    [RequirePermission(PlatformPermissions.WorkspacesManage)]
    public async Task<IActionResult> Deactivate(string key)
        => Ok(await Mediator.Send(new DeactivateWorkspaceDefinitionCommand(key)));
}

public record SetCompanyWorkspacesRequest(int TenantId, IReadOnlyList<string>? EnabledWorkspaceKeys);

[Authorize]
[ApiController]
[Route("api/platform/audit")]
public class PlatformAuditController : BaseApiController
{
    [HttpGet("catalog")]
    [RequirePermission(PlatformPermissions.AuditView)]
    public async Task<IActionResult> GetCatalog([FromQuery] bool activeOnly = false)
        => Ok(await Mediator.Send(new GetAuditCatalogQuery(activeOnly)));

    [HttpGet]
    [RequirePermission(PlatformPermissions.AuditView)]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? tenantId = null,
        [FromQuery] int? userId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? eventKey = null,
        [FromQuery] string? entityType = null,
        [FromQuery] int? entityId = null,
        [FromQuery] string? severity = null,
        [FromQuery] bool? success = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null)
        => Ok(await Mediator.Send(new SearchAuditEventsQuery(
            page, pageSize, tenantId, userId, category, eventKey, entityType, entityId,
            severity, success, fromDate, toDate, search)));

    [HttpGet("retention")]
    [RequirePermission(PlatformPermissions.AuditView)]
    public async Task<IActionResult> GetRetention([FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetAuditRetentionQuery(tenantId)));

    [HttpGet("recent")]
    [RequirePermission(PlatformPermissions.AuditView)]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int? tenantId = null,
        [FromQuery] int? userId = null,
        [FromQuery] int take = 20)
        => Ok(await Mediator.Send(new GetRecentAuditEventsQuery(tenantId, userId, take)));

    [HttpGet("export")]
    [RequirePermission(PlatformPermissions.AuditView)]
    public async Task<IActionResult> Export(
        [FromQuery] int? tenantId = null,
        [FromQuery] int? userId = null,
        [FromQuery] string? category = null,
        [FromQuery] string? eventKey = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? severity = null,
        [FromQuery] bool? success = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] string format = "csv")
    {
        var result = await Mediator.Send(new ExportAuditEventsQuery(
            tenantId, userId, category, eventKey, entityType, severity, success,
            fromDate, toDate, search, format));
        if (!result.Success || result.Data is null)
            return Ok(result);
        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    [HttpGet("{id:int}")]
    [RequirePermission(PlatformPermissions.AuditView)]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetAuditEventByIdQuery(id, tenantId)));
}

[Authorize]
[ApiController]
[Route("api/platform/security")]
public class PlatformSecurityController : BaseApiController
{
    [HttpGet]
    [RequirePermission(PlatformPermissions.SecurityView)]
    public async Task<IActionResult> GetCompanyPolicies([FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetSecurityCompanyPoliciesQuery(tenantId)));

    [HttpGet("catalog")]
    [RequirePermission(PlatformPermissions.SecurityView)]
    public async Task<IActionResult> GetCatalog([FromQuery] bool activeOnly = false)
        => Ok(await Mediator.Send(new GetSecurityCatalogQuery(activeOnly)));

    [HttpGet("company")]
    [RequirePermission(PlatformPermissions.SecurityView)]
    public async Task<IActionResult> GetCompany([FromQuery] int? tenantId = null)
        => Ok(await Mediator.Send(new GetSecurityCompanyPoliciesQuery(tenantId)));

    [HttpPut("company")]
    [RequirePermission(PlatformPermissions.SecurityManage)]
    public async Task<IActionResult> UpdateCompany([FromBody] UpdateSecurityCompanyPoliciesPayload payload)
        => Ok(await Mediator.Send(new UpdateSecurityCompanyPoliciesCommand(payload)));

    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
        => Ok(await Mediator.Send(new GetMySecuritySummaryQuery()));
}

[Authorize]
[ApiController]
[Route("api/platform/dashboards")]
public class PlatformDashboardsController : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] string? audience = null)
        => Ok(await Mediator.Send(new GetMyDashboardQuery(audience)));

    [HttpGet]
    [RequirePermission(PlatformPermissions.DashboardsView)]
    public async Task<IActionResult> GetCatalog([FromQuery] bool activeOnly = false)
        => Ok(await Mediator.Send(new GetDashboardCatalogQuery(activeOnly)));

    [HttpGet("catalog")]
    [Authorize]
    public async Task<IActionResult> GetCatalogAlias([FromQuery] bool activeOnly = false)
        => Ok(await Mediator.Send(new GetDashboardCatalogQuery(activeOnly)));

    [HttpGet("widgets")]
    [RequirePermission(PlatformPermissions.DashboardsView)]
    public async Task<IActionResult> GetWidgets([FromQuery] bool activeOnly = false)
        => Ok(await Mediator.Send(new GetDashboardWidgetsQuery(activeOnly)));

    [HttpGet("{key}")]
    [RequirePermission(PlatformPermissions.DashboardsView)]
    public async Task<IActionResult> GetByKey(string key)
        => Ok(await Mediator.Send(new GetDashboardByKeyQuery(key)));

    [HttpPut("{key}")]
    [RequirePermission(PlatformPermissions.DashboardsManage)]
    public async Task<IActionResult> UpdateDefinition(string key, [FromBody] UpdateDashboardDefinitionPayload payload)
        => Ok(await Mediator.Send(new UpdateDashboardDefinitionCommand(key, payload)));

    [HttpPut("{key}/layout")]
    [RequirePermission(PlatformPermissions.DashboardsManage)]
    public async Task<IActionResult> UpdateLayout(string key, [FromBody] UpdateDashboardLayoutPayload payload)
        => Ok(await Mediator.Send(new UpdateDashboardLayoutCommand(key, payload)));
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
        => Ok(await Mediator.Send(new UpdateRoleForTenantCommand(
            tenantId, roleId, request.Name, request.IsActive,
            request.DisplayName, request.Description, request.Category)));

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
public record UpdateRoleForTenantRequest(
    string Name,
    bool IsActive,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null);
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
[Route("api/platform/subscriptions")]
public class SubscriptionsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await Mediator.Send(new GetSubscriptionCatalogQuery()));

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog()
        => Ok(await Mediator.Send(new GetSubscriptionCatalogQuery()));

    [HttpGet("company")]
    public async Task<IActionResult> GetCompany()
        => Ok(await Mediator.Send(new GetCompanyLicenseQuery()));
}

[Authorize]
[RequirePermission(PlatformPermissions.SettingsView)]
[ApiController]
[Route("api/platform/license")]
public class LicenseController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetLicense()
        => Ok(await Mediator.Send(new GetCompanyLicenseQuery()));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
        => Ok(await Mediator.Send(new GetLicenseSummaryQuery()));
}

[RequirePermission(PlatformPermissions.TenantsManage)]
[ApiController]
[Route("api/platform/tenants/{tenantId:int}")]
public class TenantSubscriptionController : BaseApiController
{
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription(int tenantId)
        => Ok(await Mediator.Send(new GetSubscriptionOverviewQuery(tenantId)));

    [HttpGet("license")]
    public async Task<IActionResult> GetLicense(int tenantId)
        => Ok(await Mediator.Send(new GetCompanyLicenseQuery(tenantId)));

    [HttpGet("license/summary")]
    public async Task<IActionResult> GetLicenseSummary(int tenantId)
        => Ok(await Mediator.Send(new GetLicenseSummaryQuery(tenantId)));

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

[Authorize]
[ApiController]
[Route("api/platform/data-scope")]
public class DataScopeController : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
        => Ok(await Mediator.Send(new GetMyDataScopeQuery()));
}

[RequirePermission(PlatformPermissions.GpsControlView)]
[ApiController]
[Route("api/platform/gps-control")]
public class PlatformGpsControlController : BaseApiController
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
        => Ok(await Mediator.Send(new GetGpsControlDashboardQuery()));

    [HttpGet("manufacturers")]
    public async Task<IActionResult> Manufacturers()
        => Ok(await Mediator.Send(new GetGpsManufacturersQuery()));

    [RequirePermission(PlatformPermissions.GpsManufacturersManage)]
    [HttpPost("manufacturers")]
    public async Task<IActionResult> UpsertManufacturer([FromBody] GpsManufacturerDto dto)
        => Ok(await Mediator.Send(new UpsertGpsManufacturerCommand(dto)));

    [HttpGet("models")]
    public async Task<IActionResult> Models([FromQuery] int? brandId = null)
        => Ok(await Mediator.Send(new GetGpsTrackerModelsQuery(brandId)));

    [RequirePermission(PlatformPermissions.GpsModelsManage)]
    [HttpPost("models")]
    public async Task<IActionResult> UpsertModel([FromBody] GpsTrackerModelDto dto)
        => Ok(await Mediator.Send(new UpsertGpsTrackerModelCommand(dto)));

    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities()
        => Ok(await Mediator.Send(new GetGpsCapabilitiesQuery()));

    [HttpGet("models/{modelId:int}/capabilities")]
    public async Task<IActionResult> ModelCapabilities(int modelId)
        => Ok(await Mediator.Send(new GetGpsModelCapabilitiesQuery(modelId)));

    [RequirePermission(PlatformPermissions.GpsModelsManage)]
    [HttpPut("models/{modelId:int}/capabilities/{capabilityKey}")]
    public async Task<IActionResult> SetModelCapability(int modelId, string capabilityKey, [FromQuery] bool enabled = true)
        => Ok(await Mediator.Send(new SetGpsModelCapabilityCommand(modelId, capabilityKey, enabled)));

    [HttpGet("commands")]
    public async Task<IActionResult> Commands()
        => Ok(await Mediator.Send(new GetGpsCommandDefinitionsQuery()));

    [HttpGet("commands/parameters")]
    public async Task<IActionResult> CommandParameters([FromQuery] string? commandKey = null)
        => Ok(await Mediator.Send(new GetGpsCommandParametersQuery(commandKey)));

    [HttpGet("templates")]
    public async Task<IActionResult> Templates([FromQuery] int? modelId = null)
        => Ok(await Mediator.Send(new GetGpsCommandTemplatesQuery(modelId)));

    [RequirePermission(PlatformPermissions.GpsTemplatesManage)]
    [HttpPost("templates")]
    public async Task<IActionResult> UpsertTemplate([FromBody] GpsCommandTemplateDto dto)
        => Ok(await Mediator.Send(new UpsertGpsCommandTemplateCommand(dto)));

    [RequirePermission(PlatformPermissions.GpsSimulatorUse)]
    [HttpPost("translate")]
    public async Task<IActionResult> Translate([FromBody] GpsTranslateRequest request)
        => Ok(await Mediator.Send(new TranslateGpsCommandQuery(request)));

    [RequirePermission(PlatformPermissions.GpsSimulatorUse)]
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] GpsTranslateRequest request)
        => Ok(await Mediator.Send(new SimulateGpsCommandCommand(request)));

    [RequirePermission(PlatformPermissions.GpsApprove)]
    [HttpPost("commands/{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveGpsRequest body)
        => Ok(await Mediator.Send(new ApproveGpsDeviceCommandCommand(id, body.Approve, body.Note)));

    [RequirePermission(PlatformPermissions.GpsBulkExecute)]
    [HttpPost("commands/bulk")]
    public async Task<IActionResult> Bulk([FromBody] BulkExecuteGpsRequest body)
        => Ok(await Mediator.Send(new BulkExecuteGpsCommandCommand(
            body.DeviceIds, body.CommandKey, body.Parameters, body.Reason)));
}

public record ApproveGpsRequest(bool Approve, string? Note = null);

public record BulkExecuteGpsRequest(
    IReadOnlyList<int> DeviceIds,
    string CommandKey,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string? Reason = null);
