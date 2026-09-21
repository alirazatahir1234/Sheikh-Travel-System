using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Platform;

public class GetBranchesQueryHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<GetBranchesQuery, ApiResponse<IReadOnlyList<BranchDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<BranchDto>>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await platformRepository.GetBranchesAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<BranchDto>>.SuccessResponse(rows);
    }
}

public class GetBranchByIdQueryHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<GetBranchByIdQuery, ApiResponse<BranchDto>>
{
    public async Task<ApiResponse<BranchDto>> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await platformRepository.GetBranchByIdAsync(tenantId, request.Id, cancellationToken);
        if (row is null) throw new NotFoundException("Branch", request.Id);
        return ApiResponse<BranchDto>.SuccessResponse(row);
    }
}

public class CreateBranchCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<CreateBranchCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var id = await platformRepository.CreateBranchAsync(tenantId, request.Payload, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Branch created.");
    }
}

public class UpdateBranchCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateBranchCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var affected = await platformRepository.UpdateBranchAsync(tenantId, request.Id, request.Payload, cancellationToken);
        if (affected == 0) throw new NotFoundException("Branch", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Branch updated.");
    }
}

public class DeleteBranchCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<DeleteBranchCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var affected = await platformRepository.DeleteBranchAsync(tenantId, request.Id, cancellationToken);
        if (affected == 0) throw new NotFoundException("Branch", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Branch deleted.");
    }
}

public class GetDepartmentsQueryHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDepartmentsQuery, ApiResponse<IReadOnlyList<DepartmentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DepartmentDto>>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await platformRepository.GetDepartmentsAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<DepartmentDto>>.SuccessResponse(rows);
    }
}

public class CreateDepartmentCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<CreateDepartmentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payload = request.Payload;
        await platformRepository.EnsureDepartmentHeadExistsAsync(tenantId, payload.DepartmentHeadUserId, cancellationToken);
        var id = await platformRepository.CreateDepartmentAsync(
            tenantId, payload.Name.Trim(), payload.DepartmentHeadUserId, cancellationToken: cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Department created.");
    }
}

public class UpdateDepartmentCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateDepartmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payload = request.Payload;
        await platformRepository.EnsureDepartmentHeadExistsAsync(tenantId, payload.DepartmentHeadUserId, cancellationToken);
        var affected = await platformRepository.UpdateDepartmentAsync(
            tenantId, request.Id, payload.Name.Trim(), payload.DepartmentHeadUserId, request.IsActive,
            cancellationToken: cancellationToken);
        if (affected == 0) throw new NotFoundException("Department", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Department updated.");
    }
}

public class DeleteDepartmentCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<DeleteDepartmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var affected = await platformRepository.DeleteDepartmentAsync(tenantId, request.Id, cancellationToken);
        if (affected == 0) throw new NotFoundException("Department", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Department deleted.");
    }
}

public class GetPermissionsQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetPermissionsQuery, ApiResponse<IReadOnlyList<PermissionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PermissionDto>>> Handle(
        GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        var rows = await platformRepository.GetPermissionsAsync(cancellationToken);

        var enriched = rows.Select(r =>
        {
            var seed = PermissionRegistrySeed.Find(r.PermissionCode);
            return new PermissionDto(
                r.Id,
                r.ModuleName,
                r.PermissionCode,
                r.Description,
                DisplayName: r.DisplayName ?? seed?.DisplayName ?? PermissionRegistrySeed.DeriveDisplayName(r.PermissionCode),
                Category: r.Category ?? seed?.Category,
                SortOrder: r.SortOrder != 0 ? r.SortOrder : (seed?.SortOrder ?? 0),
                Visible: r.Visible,
                Action: r.Action ?? seed?.Action ?? PermissionRegistrySeed.DeriveAction(r.PermissionCode),
                ModuleKey: r.ModuleKey ?? seed?.ModuleKey);
        });

        IEnumerable<PermissionDto> filtered = enriched;
        if (!string.IsNullOrWhiteSpace(request.Category))
            filtered = filtered.Where(p => string.Equals(p.Category, request.Category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.ModuleKey))
            filtered = filtered.Where(p => string.Equals(p.ModuleKey, request.ModuleKey, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Action))
            filtered = filtered.Where(p => string.Equals(p.Action, request.Action, StringComparison.OrdinalIgnoreCase));
        if (request.Visible.HasValue)
            filtered = filtered.Where(p => p.Visible == request.Visible.Value);

        return ApiResponse<IReadOnlyList<PermissionDto>>.SuccessResponse(filtered.ToList());
    }
}

public class GetEffectivePermissionsQueryHandler(
    IPermissionEngine permissionEngine,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetEffectivePermissionsQuery, ApiResponse<IReadOnlyList<EffectivePermissionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<EffectivePermissionDto>>> Handle(
        GetEffectivePermissionsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not int userId)
            return ApiResponse<IReadOnlyList<EffectivePermissionDto>>.FailResponse("Not authenticated.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var result = await permissionEngine.EvaluateAsync(userId, tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<EffectivePermissionDto>>.SuccessResponse(result.EffectivePermissions);
    }
}

public class GetTenantModulesQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetTenantModulesQuery, ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>> Handle(
        GetTenantModulesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ModuleRegistryDto> catalog;
        try
        {
            catalog = await platformRepository.LoadModuleCatalogAsync(cancellationToken);
        }
        catch
        {
            catalog = ModuleRegistrySeed.All
                .Where(e => e.Visible)
                .OrderBy(e => e.SortOrder)
                .Select(e => ModuleRegistryQueries.FromSeed(e))
                .ToList();
        }

        var enableable = catalog
            .Where(m => m.IsEnableable)
            .Select(ModuleRegistryQueries.ToDefinitionDto)
            .ToList();

        if (enableable.Count == 0)
        {
            enableable = TenantModuleCatalog.All
                .Select(m =>
                {
                    var seed = ModuleRegistrySeed.All.FirstOrDefault(s => s.Code == m.Code);
                    var dto = seed is null
                        ? ModuleRegistryQueries.FromSeed(new ModuleRegistrySeed.Entry(
                            m.Code, m.Name, m.Name, m.Name, "Platform", "1.0.0", "extension", null, 0,
                            [], true, false, false, false, "Active", null, m.LegacyKeys))
                        : ModuleRegistryQueries.FromSeed(seed);
                    return ModuleRegistryQueries.ToDefinitionDto(dto);
                })
                .ToList();
        }

        return ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>.SuccessResponse(enableable);
    }
}

public class GetModuleCatalogQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetModuleCatalogQuery, ApiResponse<IReadOnlyList<ModuleRegistryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ModuleRegistryDto>>> Handle(
        GetModuleCatalogQuery request, CancellationToken cancellationToken)
    {
        var catalog = await platformRepository.LoadModuleCatalogAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<ModuleRegistryDto>>.SuccessResponse(catalog);
    }
}

public class GetCompanyModulesQueryHandler(
    IPlatformRepository platformRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetCompanyModulesQuery, ApiResponse<IReadOnlyList<ModuleRegistryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<ModuleRegistryDto>>> Handle(
        GetCompanyModulesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var catalog = await platformRepository.LoadModuleCatalogAsync(cancellationToken);
        var installed = (await platformRepository.GetInstalledModuleCodesAsync(tenantId, cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var planName = await platformRepository.ResolvePlanNameAsync(tenantId, cancellationToken);
        var licensed = SubscriptionPlanCatalog.LicensedModuleCodes(planName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = catalog
            .Select(m => m with
            {
                IsInstalled = installed.Contains(m.Code),
                IsLicensed = licensed.Contains(m.Code)
            })
            .ToList();

        return ApiResponse<IReadOnlyList<ModuleRegistryDto>>.SuccessResponse(result);
    }
}

public class GetModuleByKeyQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetModuleByKeyQuery, ApiResponse<ModuleRegistryDto>>
{
    public async Task<ApiResponse<ModuleRegistryDto>> Handle(
        GetModuleByKeyQuery request, CancellationToken cancellationToken)
    {
        var module = await platformRepository.LoadModuleByKeyAsync(request.CodeOrId, cancellationToken);
        if (module is null)
            return ApiResponse<ModuleRegistryDto>.FailResponse("Module not found.");
        return ApiResponse<ModuleRegistryDto>.SuccessResponse(module);
    }
}

public class GetRolesQueryHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<GetRolesQuery, ApiResponse<IReadOnlyList<RoleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var summaries = await platformRepository.LoadRoleSummariesAsync(tenantId, cancellationToken: cancellationToken);
        var dtos = summaries.Select(r => new RoleDto(
            r.Id, r.TenantId, r.Name, r.Code, r.IsSystem, r.IsActive, r.Permissions,
            r.DisplayName, r.Description, r.Category, r.RoleType, r.SortOrder, r.Visible)).ToList();
        return ApiResponse<IReadOnlyList<RoleDto>>.SuccessResponse(dtos);
    }
}

public class GetCompanyRolesQueryHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<GetCompanyRolesQuery, ApiResponse<IReadOnlyList<RoleSummaryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RoleSummaryDto>>> Handle(
        GetCompanyRolesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? platformScope.TenantId;
        platformScope.EnsureTenantAccess(tenantId);
        var rows = await platformRepository.LoadRoleSummariesAsync(
            tenantId, visibleOnly: true, cancellationToken: cancellationToken);
        return ApiResponse<IReadOnlyList<RoleSummaryDto>>.SuccessResponse(rows);
    }
}

public class CreateRoleCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<CreateRoleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await platformRepository.RoleCodeExistsAsync(tenantId, code, cancellationToken))
            throw new ConflictException($"Role code '{code}' already exists.");
        var id = await platformRepository.CreateRoleAsync(tenantId, request.Name.Trim(), code, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Role created.");
    }
}

public class UpdateRolePermissionsCommandHandler(IPlatformRepository platformRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateRolePermissionsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (!await platformRepository.RoleExistsAsync(tenantId, request.RoleId, cancellationToken))
            throw new NotFoundException("Role", request.RoleId);
        await platformRepository.ReplaceRolePermissionsAsync(
            request.RoleId, request.PermissionCodes ?? Array.Empty<string>(), cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Role permissions updated.");
    }
}


public class GetTenantsQueryHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope) : IRequestHandler<GetTenantsQuery, ApiResponse<IReadOnlyList<TenantListDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TenantListDto>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var rows = platformScope.IsSuperAdmin
            ? await platformRepository.GetAllTenantsAsync(cancellationToken)
            : await platformRepository.GetTenantByScopeAsync(platformScope.TenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<TenantListDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetTenantByIdQueryHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope) : IRequestHandler<GetTenantByIdQuery, ApiResponse<TenantDetailDto>>
{
    public async Task<ApiResponse<TenantDetailDto>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.Id);

        var row = await platformRepository.GetTenantDetailRowAsync(request.Id, cancellationToken);
        if (row is null) throw new NotFoundException("Tenant", request.Id);

        var moduleCodes = (await platformRepository.GetTenantModuleCodesOrderedAsync(request.Id, cancellationToken)).ToList();
        if (moduleCodes.Count == 0)
            moduleCodes = TenantModuleCatalog.CodesFromLegacyKeys(
                await platformRepository.GetLegacyModuleKeysAsync(request.Id, cancellationToken)).ToList();

        var adminInfo = await platformRepository.GetTenantAdminInfoAsync(request.Id, cancellationToken);
        var detail = row with { ModuleCodes = moduleCodes, AdminInfo = adminInfo };
        return ApiResponse<TenantDetailDto>.SuccessResponse(detail);
    }
}

public class ResetTenantAdminPasswordCommandHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope,
    IPasswordHasher passwordHasher) : IRequestHandler<ResetTenantAdminPasswordCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ResetTenantAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return ApiResponse<bool>.FailResponse("Password must be at least 8 characters.");

        var hash = passwordHasher.Hash(request.NewPassword);
        var rows = await platformRepository.ResetTenantAdminPasswordAsync(request.TenantId, hash, cancellationToken);
        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true)
            : ApiResponse<bool>.FailResponse("Admin user not found for this tenant.");
    }
}

public class GetTenantManagementStatsQueryHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope) : IRequestHandler<GetTenantManagementStatsQuery, ApiResponse<TenantManagementStatsDto>>
{
    public async Task<ApiResponse<TenantManagementStatsDto>> Handle(GetTenantManagementStatsQuery request, CancellationToken cancellationToken)
    {
        if (platformScope.IsSuperAdmin)
        {
            var stats = await platformRepository.GetTenantManagementStatsGlobalAsync(cancellationToken);
            return ApiResponse<TenantManagementStatsDto>.SuccessResponse(stats);
        }

        var scoped = await platformRepository.GetTenantManagementStatsScopedAsync(platformScope.TenantId, cancellationToken);
        return ApiResponse<TenantManagementStatsDto>.SuccessResponse(scoped);
    }
}

public class UpdateTenantCommandHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<UpdateTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.Id);

        IReadOnlyList<string>? moduleCodes = null;
        if (request.ModuleCodes is { Count: > 0 })
            moduleCodes = request.ModuleCodes;
        else if (request.EnabledModules is { Count: > 0 })
            moduleCodes = TenantModuleCatalog.CodesFromLegacyKeys(request.EnabledModules);

        await platformRepository.UpdateTenantAsync(
            request.Id,
            request.Name.Trim(),
            request.SubscriptionPlan,
            request.IsActive,
            request.MaxUsers,
            request.MaxVehicles,
            request.MaxDrivers,
            request.MaxBranches,
            request.MaxGpsDevices,
            moduleCodes,
            cancellationToken);

        if (moduleCodes is { Count: > 0 })
            await tenantModuleService.SyncLegacyJsonAsync(request.Id, moduleCodes, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Tenant updated.");
    }
}

public class GetUserMenuQueryHandler(
    IPlatformRepository platformRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IUserAccessService userAccessService,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<GetUserMenuQuery, ApiResponse<IReadOnlyList<MenuModuleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<MenuModuleDto>>> Handle(GetUserMenuQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var tenantId = tenantContext.GetRequiredTenantId();

        var access = await userAccessService.ResolveAsync(userId, tenantId, cancellationToken);
        var permissionSet = access.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enabledModules = await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken);
        var featureFlags = await platformRepository.LoadTenantFeatureFlagsAsync(tenantId, cancellationToken);
        var (modules, menus) = await platformRepository.LoadNavTablesAsync(activeMenusOnly: true, cancellationToken: cancellationToken);

        IReadOnlyList<string>? workspaceModuleKeys = null;
        try
        {
            var profile = await platformRepository.GetUserWorkspaceProfileAsync(userId, tenantId, cancellationToken);
            var catalog = await platformRepository.LoadWorkspaceCatalogAsync(activeOnly: true, cancellationToken: cancellationToken);
            var flags = await platformRepository.LoadTenantWorkspaceFlagsAsync(tenantId, cancellationToken);
            var resolved = WorkspaceBuilderQueries.Resolve(
                catalog, flags, profile?.DefaultWorkspaceKey, profile?.HomeRoute, profile?.RoleCode ?? access.RoleCodes.FirstOrDefault());
            workspaceModuleKeys = resolved.ModuleKeys;
        }
        catch
        {
            // Workspace catalog optional until Stage 10 migration applied.
        }

        var result = MenuBuilderQueries.BuildUserMenu(
            modules, menus, permissionSet, enabledModules, featureFlags, workspaceModuleKeys);
        return ApiResponse<IReadOnlyList<MenuModuleDto>>.SuccessResponse(result);
    }
}

public class GetOrganizationTreeQueryHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope) : IRequestHandler<GetOrganizationTreeQuery, ApiResponse<OrganizationTreeDto>>
{
    public async Task<ApiResponse<OrganizationTreeDto>> Handle(GetOrganizationTreeQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        var tenantName = await platformRepository.GetTenantNameAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantId);

        var branches = await platformRepository.GetOrganizationBranchesAsync(request.TenantId, cancellationToken);
        var departments = await platformRepository.GetOrganizationDepartmentsAsync(request.TenantId, cancellationToken);
        var deptLookup = departments.ToLookup(d => d.BranchId);

        var branchDtos = branches.Select(b => new OrganizationBranchDto(
            b.Id, b.ParentBranchId, b.BranchCode, b.Name, b.BranchType, b.City, b.Country, b.IsActive, b.Status,
            deptLookup[b.Id].Select(d => new OrganizationDepartmentDto(d.Id, d.BranchId, d.Name, d.DepartmentHeadName, d.StaffCount, d.IsActive)).ToList()
        )).ToList();

        var unassignedDepts = deptLookup[null]
            .Select(d => new OrganizationDepartmentDto(d.Id, d.BranchId, d.Name, d.DepartmentHeadName, d.StaffCount, d.IsActive))
            .ToList();

        return ApiResponse<OrganizationTreeDto>.SuccessResponse(
            new OrganizationTreeDto(request.TenantId, tenantName, branchDtos, unassignedDepts));
    }
}

public class GetBranchesForTenantQueryHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<GetBranchesForTenantQuery, ApiResponse<IReadOnlyList<BranchDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<BranchDto>>> Handle(GetBranchesForTenantQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var rows = await platformRepository.GetBranchesAsync(request.TenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<BranchDto>>.SuccessResponse(rows);
    }
}

public class CreateBranchForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<CreateBranchForTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateBranchForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var id = await platformRepository.CreateBranchAsync(request.TenantId, request.Payload, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Branch created.");
    }
}

public class UpdateBranchForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<UpdateBranchForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateBranchForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var affected = await platformRepository.UpdateBranchAsync(request.TenantId, request.BranchId, request.Payload, cancellationToken);
        if (affected == 0) throw new NotFoundException("Branch", request.BranchId);
        return ApiResponse<bool>.SuccessResponse(true, "Branch updated.");
    }
}

public class DeleteBranchForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<DeleteBranchForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteBranchForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        await platformRepository.UnassignDepartmentsFromBranchAsync(request.TenantId, request.BranchId, cancellationToken);
        var affected = await platformRepository.DeleteBranchAsync(request.TenantId, request.BranchId, cancellationToken);
        if (affected == 0) throw new NotFoundException("Branch", request.BranchId);
        return ApiResponse<bool>.SuccessResponse(true, "Branch deleted.");
    }
}

public class GetDepartmentsForTenantQueryHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<GetDepartmentsForTenantQuery, ApiResponse<IReadOnlyList<DepartmentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DepartmentDto>>> Handle(GetDepartmentsForTenantQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var rows = await platformRepository.GetDepartmentsAsync(request.TenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<DepartmentDto>>.SuccessResponse(rows);
    }
}

public class CreateDepartmentForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<CreateDepartmentForTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDepartmentForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var payload = request.Payload;
        await platformRepository.EnsureDepartmentHeadExistsAsync(request.TenantId, payload.DepartmentHeadUserId, cancellationToken);
        if (payload.BranchId.HasValue)
            await platformRepository.EnsureBranchExistsAsync(request.TenantId, payload.BranchId.Value, cancellationToken);

        var id = await platformRepository.CreateDepartmentAsync(
            request.TenantId, payload.Name.Trim(), payload.DepartmentHeadUserId, payload.BranchId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Department created.");
    }
}

public class UpdateDepartmentForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<UpdateDepartmentForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDepartmentForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var payload = request.Payload;
        await platformRepository.EnsureDepartmentHeadExistsAsync(request.TenantId, payload.DepartmentHeadUserId, cancellationToken);
        if (payload.BranchId.HasValue)
            await platformRepository.EnsureBranchExistsAsync(request.TenantId, payload.BranchId.Value, cancellationToken);

        var affected = await platformRepository.UpdateDepartmentAsync(
            request.TenantId, request.DepartmentId, payload.Name.Trim(), payload.DepartmentHeadUserId, request.IsActive,
            payload.BranchId, includeBranchId: true, cancellationToken);
        if (affected == 0) throw new NotFoundException("Department", request.DepartmentId);
        return ApiResponse<bool>.SuccessResponse(true, "Department updated.");
    }
}

public class DeleteDepartmentForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<DeleteDepartmentForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteDepartmentForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var affected = await platformRepository.DeleteDepartmentAsync(request.TenantId, request.DepartmentId, cancellationToken);
        if (affected == 0) throw new NotFoundException("Department", request.DepartmentId);
        return ApiResponse<bool>.SuccessResponse(true, "Department deleted.");
    }
}

public class MoveDepartmentCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<MoveDepartmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MoveDepartmentCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        if (request.NewBranchId.HasValue)
            await platformRepository.EnsureBranchExistsAsync(request.TenantId, request.NewBranchId.Value, cancellationToken);

        var affected = await platformRepository.MoveDepartmentAsync(
            request.TenantId, request.DepartmentId, request.NewBranchId, cancellationToken);
        if (affected == 0) throw new NotFoundException("Department", request.DepartmentId);
        return ApiResponse<bool>.SuccessResponse(true, "Department moved.");
    }
}

public class GetRolesForTenantQueryHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<GetRolesForTenantQuery, ApiResponse<IReadOnlyList<RoleSummaryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<RoleSummaryDto>>> Handle(
        GetRolesForTenantQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var rows = await platformRepository.LoadRoleSummariesAsync(request.TenantId, cancellationToken: cancellationToken);
        return ApiResponse<IReadOnlyList<RoleSummaryDto>>.SuccessResponse(rows);
    }
}

public class CreateRoleForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<CreateRoleForTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateRoleForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await platformRepository.RoleCodeExistsAsync(request.TenantId, code, cancellationToken))
            throw new ConflictException($"Role code '{code}' already exists.");
        var id = await platformRepository.CreateRoleForTenantAsync(request.TenantId, request.Name.Trim(), code, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Role created.");
    }
}

public class UpdateRoleForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<UpdateRoleForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRoleForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var role = await platformRepository.GetRoleMetaAsync(request.TenantId, request.RoleId, cancellationToken);
        if (role is null) throw new NotFoundException("Role", request.RoleId);

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Name.Trim() : request.DisplayName.Trim();
        await platformRepository.UpdateRoleForTenantAsync(
            request.TenantId, request.RoleId, request.Name.Trim(), request.IsActive,
            displayName, request.Description?.Trim(), request.Category?.Trim(), cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Role updated.");
    }
}

public class DeleteRoleForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<DeleteRoleForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteRoleForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var role = await platformRepository.GetRoleMetaAsync(request.TenantId, request.RoleId, cancellationToken);
        if (role is null) throw new NotFoundException("Role", request.RoleId);
        if (role.Value.IsSystem) throw new ConflictException("System roles cannot be deleted.");

        var userCount = await platformRepository.CountUsersWithRoleAsync(request.RoleId, cancellationToken);
        if (userCount > 0) throw new ConflictException("Cannot delete a role that is assigned to users.");

        var affected = await platformRepository.DeleteRoleAsync(request.TenantId, request.RoleId, cancellationToken);
        if (affected == 0) throw new NotFoundException("Role", request.RoleId);
        return ApiResponse<bool>.SuccessResponse(true, "Role deleted.");
    }
}

public class UpdateRolePermissionsForTenantCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<UpdateRolePermissionsForTenantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateRolePermissionsForTenantCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        if (!await platformRepository.RoleExistsAsync(request.TenantId, request.RoleId, cancellationToken))
            throw new NotFoundException("Role", request.RoleId);
        await platformRepository.ReplaceRolePermissionsAsync(
            request.RoleId, request.PermissionCodes ?? Array.Empty<string>(), cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Role permissions updated.");
    }
}

public class GetTenantSecuritySettingsQueryHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<GetTenantSecuritySettingsQuery, ApiResponse<TenantSecuritySettingsDto>>
{
    public async Task<ApiResponse<TenantSecuritySettingsDto>> Handle(
        GetTenantSecuritySettingsQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var row = await platformRepository.GetTenantSecuritySettingsAsync(request.TenantId, cancellationToken)
            ?? new TenantSecuritySettingsDto(false, 90, 30, true, true, false);
        return ApiResponse<TenantSecuritySettingsDto>.SuccessResponse(row);
    }
}

public class UpdateTenantSecuritySettingsCommandHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope,
    ISecurityEngine securityEngine,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateTenantSecuritySettingsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateTenantSecuritySettingsCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var p = request.Payload;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SecurityPolicyKeys.ComplianceMfaRequired] = p.IsMfaRequired ? "true" : "false",
            [SecurityPolicyKeys.PasswordMaxAgeDays] = (p.PasswordExpiryDays ?? 0).ToString(),
            [SecurityPolicyKeys.SessionIdleTimeoutMinutes] = (p.SessionTimeoutMinutes ?? 30).ToString(),
            [SecurityPolicyKeys.ComplianceGdprLogging] = p.IsGdprEnabled ? "true" : "false",
            [SecurityPolicyKeys.AuditLevel] = p.IsAuditLoggingEnabled ? "Always" : "Disabled",
            [SecurityPolicyKeys.ComplianceVatEnabled] = p.IsVatEnabled ? "true" : "false"
        };

        try
        {
            await securityEngine.SetCompanyPoliciesAsync(
                request.TenantId, values, currentUser.UserId, cancellationToken);
        }
        catch
        {
            // Security registry may not be migrated yet — legacy write below still applies.
        }

        await platformRepository.UpsertTenantSecuritySettingsAsync(request.TenantId, p, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Security settings updated.");
    }
}

public class GetRoleTemplatesQueryHandler
    : IRequestHandler<GetRoleTemplatesQuery, ApiResponse<IReadOnlyList<RoleTemplateDto>>>
{
    public Task<ApiResponse<IReadOnlyList<RoleTemplateDto>>> Handle(
        GetRoleTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = TenantRolePermissionTemplates.StandardRoles
            .Select(t =>
            {
                var seed = RoleRegistrySeed.Find(t.RoleCode);
                var name = seed?.Name ?? t.RoleCode.Replace('_', ' ');
                return new RoleTemplateDto(
                    t.RoleCode,
                    name,
                    t.Permissions.Length,
                    t.Permissions,
                    seed?.DisplayName ?? name,
                    seed?.Description,
                    seed?.Category);
            })
            .ToList();

        return Task.FromResult(ApiResponse<IReadOnlyList<RoleTemplateDto>>.SuccessResponse(templates));
    }
}

public class ApplyRoleTemplateCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<ApplyRoleTemplateCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ApplyRoleTemplateCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        var template = TenantRolePermissionTemplates.StandardRoles
            .FirstOrDefault(t => string.Equals(t.RoleCode, request.RoleCode, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(template.RoleCode))
            throw new NotFoundException("Role template", request.RoleCode);

        var code = template.RoleCode.ToUpperInvariant();
        var seed = RoleRegistrySeed.Find(code);
        var name = seed?.Name ?? code.Replace('_', ' ');

        await platformRepository.ApplyRoleTemplateAsync(
            request.TenantId, code, name, seed, template.Permissions, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, $"Template '{name}' applied.");
    }
}

public class GetTenantModuleOverviewQueryHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<GetTenantModuleOverviewQuery, ApiResponse<TenantModuleOverviewDto>>
{
    public async Task<ApiResponse<TenantModuleOverviewDto>> Handle(
        GetTenantModuleOverviewQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        var tenant = await platformRepository.GetTenantNameAndPlanAsync(request.TenantId, cancellationToken);
        if (tenant is null) throw new NotFoundException("Tenant", request.TenantId);

        var enabledCodes = (await platformRepository.GetInstalledModuleCodesAsync(request.TenantId, cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (enabledCodes.Count == 0)
        {
            var legacyJson = await platformRepository.GetEnabledModulesJsonAsync(request.TenantId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(legacyJson))
            {
                try
                {
                    var keys = System.Text.Json.JsonSerializer.Deserialize<string[]>(legacyJson) ?? [];
                    foreach (var code in TenantModuleCatalog.CodesFromLegacyKeys(keys))
                        enabledCodes.Add(code);
                }
                catch { /* ignore malformed legacy json */ }
            }
        }

        var catalog = await platformRepository.LoadModuleCatalogAsync(cancellationToken);
        var planName = await platformRepository.ResolvePlanNameAsync(request.TenantId, cancellationToken);
        var licensedCodes = SubscriptionPlanCatalog.LicensedModuleCodes(planName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var modules = catalog
            .Select(m => ModuleRegistryQueries.ToStatusDto(
                m, enabledCodes.Contains(m.Code), licensedCodes.Contains(m.Code)))
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Code)
            .ToList();

        if (modules.Count == 0)
        {
            modules = TenantModuleCatalog.All
                .Select(m =>
                {
                    var seed = ModuleRegistrySeed.All.FirstOrDefault(s => s.Code == m.Code);
                    var dto = seed is null
                        ? ModuleRegistryQueries.FromSeed(new ModuleRegistrySeed.Entry(
                            m.Code, m.Name, m.Name, m.Name, "Platform", "1.0.0", "extension", null, 0,
                            [], true, false, false, false, "Active", null, m.LegacyKeys))
                        : ModuleRegistryQueries.FromSeed(seed);
                    return ModuleRegistryQueries.ToStatusDto(
                        dto, enabledCodes.Contains(m.Code), licensedCodes.Contains(m.Code));
                })
                .ToList();
        }

        var limits = await platformRepository.GetTenantModuleLimitsAsync(request.TenantId, cancellationToken);
        var licenseLimits = new List<LicenseLimitDto>
        {
            new("Users", limits.UsedUsers, limits.MaxUsers),
            new("Vehicles", limits.UsedVehicles, limits.MaxVehicles),
            new("Drivers", limits.UsedDrivers, limits.MaxDrivers),
            new("Branches", limits.UsedBranches, limits.MaxBranches),
            new("GPS Devices", limits.UsedGps, limits.MaxGpsDevices),
        };

        return ApiResponse<TenantModuleOverviewDto>.SuccessResponse(
            new TenantModuleOverviewDto(request.TenantId, tenant.Value.Name, tenant.Value.PlanName, modules, licenseLimits));
    }
}

public class SetTenantModulesCommandHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<SetTenantModulesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetTenantModulesCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        await platformRepository.EnsureTenantExistsAsync(request.TenantId, cancellationToken);

        var validCodes = TenantModuleCatalog.All.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var moduleCodes = (request.ModuleCodes ?? Array.Empty<string>())
            .Where(c => validCodes.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await platformRepository.ReplaceTenantModulesAsync(request.TenantId, moduleCodes, cancellationToken);
        await tenantModuleService.SyncLegacyJsonAsync(request.TenantId, moduleCodes, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Modules updated.");
    }
}

public class GetSubscriptionCatalogQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetSubscriptionCatalogQuery, ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>> Handle(
        GetSubscriptionCatalogQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var list = await platformRepository.LoadSubscriptionPlansFromDbAsync(cancellationToken);
            if (list.Count > 0)
                return ApiResponse<IReadOnlyList<SubscriptionPlanDto>>.SuccessResponse(list);
        }
        catch
        {
            // Table may not exist yet.
        }

        return ApiResponse<IReadOnlyList<SubscriptionPlanDto>>.SuccessResponse(LicenseQueries.FromSeed());
    }
}

public class GetCompanyLicenseQueryHandler(
    IPlatformRepository platformRepository,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetCompanyLicenseQuery, ApiResponse<CompanyLicenseDto>>
{
    public async Task<ApiResponse<CompanyLicenseDto>> Handle(
        GetCompanyLicenseQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? tenantContext.GetRequiredTenantId();
        platformScope.EnsureTenantAccess(tenantId);
        var license = await platformRepository.LoadCompanyLicenseAsync(tenantId, cancellationToken);
        if (license is null)
            return ApiResponse<CompanyLicenseDto>.FailResponse("Company not found.");
        return ApiResponse<CompanyLicenseDto>.SuccessResponse(license);
    }
}

public class GetLicenseSummaryQueryHandler(
    IPlatformRepository platformRepository,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetLicenseSummaryQuery, ApiResponse<LicenseSummaryDto>>
{
    public async Task<ApiResponse<LicenseSummaryDto>> Handle(
        GetLicenseSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? tenantContext.GetRequiredTenantId();
        platformScope.EnsureTenantAccess(tenantId);
        var license = await platformRepository.LoadCompanyLicenseAsync(tenantId, cancellationToken);
        if (license is null)
            return ApiResponse<LicenseSummaryDto>.FailResponse("Company not found.");
        return ApiResponse<LicenseSummaryDto>.SuccessResponse(LicenseQueries.ToSummary(license));
    }
}

public class GetSubscriptionOverviewQueryHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<GetSubscriptionOverviewQuery, ApiResponse<SubscriptionOverviewDto>>
{
    public async Task<ApiResponse<SubscriptionOverviewDto>> Handle(
        GetSubscriptionOverviewQuery request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        var subscription = await platformRepository.GetSubscriptionDetailAsync(request.TenantId, cancellationToken);
        if (subscription is null) throw new NotFoundException("Tenant", request.TenantId);

        var licensed = SubscriptionPlanCatalog.LicensedModuleCodes(
            subscription.SubscriptionCode ?? subscription.PlanName);
        subscription = subscription with { LicensedModuleCodes = licensed.ToList() };

        var invoices = await platformRepository.GetTenantInvoicesAsync(request.TenantId, cancellationToken);
        var payments = await platformRepository.GetTenantBillingHistoryAsync(request.TenantId, cancellationToken);
        var license = await platformRepository.LoadCompanyLicenseAsync(request.TenantId, cancellationToken);

        return ApiResponse<SubscriptionOverviewDto>.SuccessResponse(
            new SubscriptionOverviewDto(subscription, invoices, payments, license));
    }
}

public class UpdateSubscriptionCommandHandler(IPlatformRepository platformRepository, IPlatformScope platformScope)
    : IRequestHandler<UpdateSubscriptionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);
        await platformRepository.EnsureTenantExistsAsync(request.TenantId, cancellationToken);
        await platformRepository.ApplySubscriptionActionAsync(
            request.TenantId,
            request.Action,
            request.PlanName,
            request.MonthlyAmount,
            request.BillingCycle,
            request.AutoRenew,
            cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, $"Subscription {request.Action.ToString().ToLowerInvariant()}d.");
    }
}
