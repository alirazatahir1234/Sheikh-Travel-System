using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Company;

public record CompanyFeatureDto(
    string FeatureKey,
    string ModuleKey,
    string Name,
    string? Description,
    bool IsEnabled,
    int SortOrder,
    string? DisplayName = null,
    string? Category = null,
    string? Icon = null,
    string? Route = null,
    string? Status = null,
    bool IsMobileSupported = false,
    bool IsAISupported = false,
    bool IsGPSSupported = false,
    bool Visible = true,
    bool CanToggle = false);

public record CompanyModuleDto(
    string ModuleCode,
    string Name,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? Version = null,
    string? Icon = null,
    string? Status = null,
    bool IsMobileSupported = false,
    bool IsAISupported = false,
    bool IsGPSSupported = false);

public record CompanyHierarchyCountsDto(
    int BranchCount,
    int DepartmentCount,
    int UserCount,
    int DriverCount,
    int VehicleCount,
    int ModuleCount,
    int FeatureCount);

public record CompanySubscriptionDto(
    string? SubscriptionCode,
    string? PlanName,
    string? PlanDisplayName,
    string Status,
    DateTime? StartDate,
    DateTime? EndDate,
    bool AutoRenew,
    IReadOnlyList<string> LicensedModuleCodes,
    int? MaxUsers,
    int? MaxDrivers,
    int? MaxVehicles,
    int? MaxBranches,
    int? MaxGpsDevices,
    int? StorageQuotaGb,
    int? AICredits,
    bool GPSEnabled,
    int UsedUsers = 0,
    int UsedDrivers = 0,
    int UsedVehicles = 0);

public record CompanyCurrentUserDto(
    string? JobTitle,
    string? EmployeeType,
    string? Status,
    string? DefaultWorkspaceKey,
    string? DefaultDashboardKey,
    string? HomeRoute,
    string? Language,
    string? Theme,
    string? AvatarUrl,
    string? EmployeeCode = null);

public record CompanyContextDto(
    int CompanyId,
    int TenantId,
    string CompanyName,
    string Slug,
    string? LogoUrl,
    string? PrimaryColor,
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    IReadOnlyList<string> EnabledModuleKeys,
    IReadOnlyList<CompanyModuleDto> Modules,
    IReadOnlyList<CompanyFeatureDto> Features,
    CompanyHierarchyCountsDto Hierarchy,
    string? WorkspaceHint,
    string? RoleCode,
    CompanySubscriptionDto? Subscription = null,
    CompanyCurrentUserDto? CurrentUser = null,
    IReadOnlyList<AssignedRoleDto>? AssignedRoles = null,
    IReadOnlyList<EffectivePermissionDto>? EffectivePermissions = null,
    CompanyNavSummaryDto? NavSummary = null,
    ResolvedWorkspaceDto? Workspace = null,
    CompanyDashboardSummaryDto? Dashboard = null,
    CompanyDataScopeDto? DataScope = null,
    SecurityCompanySummaryDto? Security = null,
    AuditCompanySummaryDto? Audit = null);

public record GetCompanyContextQuery : IRequest<ApiResponse<CompanyContextDto>>;

public record FeatureDefinitionDto(
    string FeatureKey,
    string ModuleKey,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    string? DisplayName = null,
    string? Category = null,
    string? Icon = null,
    string? Route = null,
    string? Status = null,
    bool Visible = true,
    bool IsMobileSupported = false,
    bool IsAISupported = false,
    bool IsGPSSupported = false,
    string? DocumentationUrl = null);

public record GetFeatureCatalogQuery : IRequest<ApiResponse<IReadOnlyList<FeatureDefinitionDto>>>;

public record GetCompanyFeaturesQuery(int? TenantId = null)
    : IRequest<ApiResponse<IReadOnlyList<CompanyFeatureDto>>>;

public class GetCompanyContextQueryHandler(
    ICompanyRepository companyRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantModuleService tenantModuleService,
    IPermissionEngine permissionEngine,
    IDataScopeEngine dataScopeEngine,
    ISecurityEngine securityEngine,
    IAuditEngine auditEngine)
    : IRequestHandler<GetCompanyContextQuery, ApiResponse<CompanyContextDto>>
{
    public async Task<ApiResponse<CompanyContextDto>> Handle(
        GetCompanyContextQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var company = await companyRepository.GetTenantAsync(tenantId, cancellationToken);

        if (company is null || company.Id == 0)
            return ApiResponse<CompanyContextDto>.FailResponse("Company not found.");

        int? branchId = null;
        string? branchName = null;
        int? departmentId = null;
        string? departmentName = null;
        string? roleCode = currentUser.Role;
        CompanyCurrentUserDto? currentUserProfile = null;
        IReadOnlyList<AssignedRoleDto>? assignedRoles = null;
        IReadOnlyList<EffectivePermissionDto>? effectivePermissions = null;
        CompanyNavSummaryDto? navSummary = null;
        ResolvedWorkspaceDto? resolvedWorkspace = null;
        CompanyDashboardSummaryDto? dashboardSummary = null;
        CompanyDataScopeDto? dataScopeDto = null;
        SecurityCompanySummaryDto? securitySummary = null;
        AuditCompanySummaryDto? auditSummary = null;
        DateTime? passwordChangedAt = null;

        if (currentUser.UserId is int userId)
        {
            try
            {
                var org = await companyRepository.GetUserOrgProfileAsync(userId, tenantId, cancellationToken)
                    ?? new CompanyUserOrgProfileRow(
                        null, null, null, null, null, null, null, null, null, null,
                        null, null, null, null, null);

                branchId = org.BranchId;
                branchName = org.BranchName;
                departmentId = org.DepartmentId;
                departmentName = org.DepartmentName;
                if (!string.IsNullOrWhiteSpace(org.RoleCode))
                    roleCode = org.RoleCode;

                passwordChangedAt = await companyRepository.GetPasswordChangedAtAsync(userId, cancellationToken);

                currentUserProfile = new CompanyCurrentUserDto(
                    org.JobTitle,
                    org.EmployeeType,
                    org.Status,
                    org.DefaultWorkspaceKey,
                    org.DefaultDashboardKey,
                    org.HomeRoute,
                    org.Language,
                    org.Theme,
                    org.AvatarUrl,
                    org.EmployeeCode);

                assignedRoles = await companyRepository.GetAssignedRolesAsync(userId, cancellationToken);
                if (assignedRoles.Count > 0 && string.IsNullOrWhiteSpace(roleCode))
                    roleCode = assignedRoles[0].Code;

                try
                {
                    var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
                    dataScopeDto = await companyRepository.MapDataScopeAsync(scope, cancellationToken);
                }
                catch
                {
                    // Data scope migration optional.
                }

                try
                {
                    securitySummary = await securityEngine.GetSafeSummaryAsync(
                        tenantId, passwordChangedAt, cancellationToken);
                }
                catch
                {
                    // Security registry optional until migration applied.
                }

                try
                {
                    auditSummary = await auditEngine.GetSafeSummaryAsync(tenantId, cancellationToken);
                }
                catch
                {
                    // Audit registry optional until migration applied.
                }

                try
                {
                    var eval = await permissionEngine.EvaluateAsync(userId, tenantId, cancellationToken);
                    effectivePermissions = eval.EffectivePermissions
                        .Select(p => new EffectivePermissionDto(
                            p.Code, p.DisplayName, p.Category, p.ModuleKey, p.Action, null))
                        .Take(200)
                        .ToList();

                    var enabledLegacy = await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken);
                    var permissionSet = eval.EffectivePermissions
                        .Select(p => p.Code)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    IReadOnlyList<string>? workspaceModuleKeys = null;
                    try
                    {
                        resolvedWorkspace = await companyRepository.ResolveWorkspaceAsync(
                            tenantId,
                            org.DefaultWorkspaceKey,
                            org.HomeRoute,
                            roleCode,
                            cancellationToken);
                        workspaceModuleKeys = resolvedWorkspace?.ModuleKeys;
                    }
                    catch
                    {
                        resolvedWorkspace = null;
                    }

                    var (builtNav, featureFlags) = await companyRepository.BuildNavSummaryAsync(
                        tenantId,
                        permissionSet,
                        enabledLegacy,
                        workspaceModuleKeys,
                        cancellationToken);
                    navSummary = builtNav;

                    try
                    {
                        var preferMobile = IsMobilePreferringRole(roleCode)
                            || (org.DefaultDashboardKey?.StartsWith("mobile.", StringComparison.OrdinalIgnoreCase) ?? false);
                        dashboardSummary = await companyRepository.ResolveDashboardAsync(
                            org.DefaultDashboardKey,
                            resolvedWorkspace?.DefaultDashboardKey,
                            resolvedWorkspace?.Key ?? org.DefaultWorkspaceKey,
                            roleCode,
                            preferMobile,
                            permissionSet,
                            enabledLegacy.ToHashSet(StringComparer.OrdinalIgnoreCase),
                            featureFlags,
                            cancellationToken);
                    }
                    catch
                    {
                        dashboardSummary = null;
                    }
                }
                catch
                {
                    // Engine / menu metadata optional if migration not applied yet.
                }
            }
            catch
            {
                var org = await companyRepository.GetUserOrgFallbackAsync(userId, tenantId, cancellationToken);
                branchId = org?.BranchId;
                branchName = org?.BranchName;
                departmentId = org?.DepartmentId;
                departmentName = org?.DepartmentName;
                if (!string.IsNullOrWhiteSpace(org?.RoleCode))
                    roleCode = org.RoleCode;
            }
        }

        if (auditSummary is null)
        {
            try
            {
                auditSummary = await auditEngine.GetSafeSummaryAsync(tenantId, cancellationToken);
            }
            catch
            {
                // Audit registry optional.
            }
        }

        var moduleCodes = (await companyRepository.GetTenantModulesAsync(tenantId, cancellationToken)).ToList();

        IReadOnlyList<string> enabledLegacyKeys;
        if (moduleCodes.Count == 0)
        {
            enabledLegacyKeys = await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken);
            moduleCodes = TenantModuleCatalog.All
                .Where(m => enabledLegacyKeys.Any(k =>
                    m.LegacyKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
                .Select(m =>
                {
                    var seed = ModuleRegistrySeed.All.FirstOrDefault(s => s.Code == m.Code);
                    return new CompanyModuleRow(
                        ModuleCode: m.Code,
                        Name: m.Name,
                        DisplayName: seed?.DisplayName ?? m.Name,
                        Description: seed?.Description,
                        Category: seed?.Category,
                        Version: seed?.Version ?? "1.0.0",
                        Icon: seed?.Icon,
                        Status: seed?.Status ?? "Active",
                        IsMobileSupported: seed?.IsMobileSupported ?? false,
                        IsAISupported: seed?.IsAISupported ?? false,
                        IsGPSSupported: seed?.IsGPSSupported ?? false);
                })
                .ToList();
        }
        else
        {
            enabledLegacyKeys = TenantModuleCatalog.LegacyKeysFromCodes(moduleCodes.Select(m => m.ModuleCode));
        }

        var moduleDtos = moduleCodes
            .Select(m => new CompanyModuleDto(
                m.ModuleCode,
                m.Name,
                m.DisplayName,
                m.Description,
                m.Category,
                m.Version,
                m.Icon,
                m.Status,
                m.IsMobileSupported,
                m.IsAISupported,
                m.IsGPSSupported))
            .ToList();

        var features = await companyRepository.GetEnabledCompanyFeaturesAsync(
            tenantId,
            moduleCodes.Select(m => m.ModuleCode).ToList(),
            cancellationToken);

        var counts = await companyRepository.GetHierarchyCountsAsync(
            tenantId,
            moduleCodes.Count,
            features.Count,
            cancellationToken);

        var workspaceHint = !string.IsNullOrWhiteSpace(currentUserProfile?.DefaultWorkspaceKey)
            ? currentUserProfile!.DefaultWorkspaceKey
            : ResolveWorkspaceHint(roleCode);

        if (resolvedWorkspace is null)
        {
            resolvedWorkspace = await companyRepository.ResolveWorkspaceAsync(
                tenantId,
                currentUserProfile?.DefaultWorkspaceKey,
                currentUserProfile?.HomeRoute,
                roleCode,
                cancellationToken);

            if (resolvedWorkspace is null)
            {
                resolvedWorkspace = new ResolvedWorkspaceDto(
                    workspaceHint ?? "home",
                    workspaceHint ?? "Home",
                    currentUserProfile?.HomeRoute ?? "/dashboard",
                    null,
                    currentUserProfile?.DefaultDashboardKey,
                    "default",
                    Array.Empty<string>());
            }
        }

        if (resolvedWorkspace is not null)
            workspaceHint = resolvedWorkspace.Key;

        var subscription = await companyRepository.GetSubscriptionAsync(tenantId, cancellationToken);

        var dto = new CompanyContextDto(
            CompanyId: company.Id,
            TenantId: company.Id,
            CompanyName: company.Name,
            Slug: company.Slug,
            LogoUrl: company.LogoUrl,
            PrimaryColor: company.PrimaryColor,
            BranchId: branchId,
            BranchName: branchName,
            DepartmentId: departmentId,
            DepartmentName: departmentName,
            EnabledModuleKeys: enabledLegacyKeys.ToList(),
            Modules: moduleDtos,
            Features: features,
            Hierarchy: counts,
            WorkspaceHint: workspaceHint,
            RoleCode: roleCode,
            Subscription: subscription,
            CurrentUser: currentUserProfile,
            AssignedRoles: assignedRoles,
            EffectivePermissions: effectivePermissions,
            NavSummary: navSummary,
            Workspace: resolvedWorkspace,
            Dashboard: dashboardSummary,
            DataScope: dataScopeDto,
            Security: securitySummary,
            Audit: auditSummary);

        return ApiResponse<CompanyContextDto>.SuccessResponse(dto);
    }

    private static string ResolveWorkspaceHint(string? roleCode)
        => WorkspaceBuilderQueries.RoleHint(roleCode);

    private static bool IsMobilePreferringRole(string? roleCode) =>
        roleCode?.ToUpperInvariant() is "DRIVER" or "FIELD_DRIVER" or "FLEET_MANAGER"
            or "DRIVER_MANAGER" or "DISPATCHER" or "SUPER_ADMIN" or "TENANT_ADMIN" or "ADMIN";
}

public class GetFeatureCatalogQueryHandler(ICompanyRepository companyRepository)
    : IRequestHandler<GetFeatureCatalogQuery, ApiResponse<IReadOnlyList<FeatureDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<FeatureDefinitionDto>>> Handle(
        GetFeatureCatalogQuery request,
        CancellationToken cancellationToken)
    {
        var dtos = await companyRepository.GetFeatureCatalogAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<FeatureDefinitionDto>>.SuccessResponse(dtos);
    }
}

public class GetCompanyFeaturesQueryHandler(
    ICompanyRepository companyRepository,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetCompanyFeaturesQuery, ApiResponse<IReadOnlyList<CompanyFeatureDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<CompanyFeatureDto>>> Handle(
        GetCompanyFeaturesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? tenantContext.GetRequiredTenantId();
        platformScope.EnsureTenantAccess(tenantId);

        var dtos = await companyRepository.GetCompanyFeaturesAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<CompanyFeatureDto>>.SuccessResponse(dtos);
    }
}
