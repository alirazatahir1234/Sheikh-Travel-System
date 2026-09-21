using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for platform / company-admin features. SQL lives in Infrastructure.
/// </summary>
public interface IPlatformRepository
{
    // ── Branches ──────────────────────────────────────────────────────────
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<BranchDto?> GetBranchByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<int> CreateBranchAsync(int tenantId, BranchUpsertPayload payload, CancellationToken cancellationToken = default);
    Task<int> UpdateBranchAsync(int tenantId, int id, BranchUpsertPayload payload, CancellationToken cancellationToken = default);
    Task<int> DeleteBranchAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task UnassignDepartmentsFromBranchAsync(int tenantId, int branchId, CancellationToken cancellationToken = default);

    // ── Departments ───────────────────────────────────────────────────────
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<int> CreateDepartmentAsync(int tenantId, string name, int? departmentHeadUserId, int? branchId = null, CancellationToken cancellationToken = default);
    Task<int> UpdateDepartmentAsync(int tenantId, int id, string name, int? departmentHeadUserId, bool isActive, int? branchId = null, bool includeBranchId = false, CancellationToken cancellationToken = default);
    Task<int> DeleteDepartmentAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task EnsureDepartmentHeadExistsAsync(int tenantId, int? headUserId, CancellationToken cancellationToken = default);
    Task EnsureBranchExistsAsync(int tenantId, int branchId, CancellationToken cancellationToken = default);
    Task<int> MoveDepartmentAsync(int tenantId, int departmentId, int? newBranchId, CancellationToken cancellationToken = default);

    // ── Organization tree ─────────────────────────────────────────────────
    Task<string?> GetTenantNameAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationBranchRow>> GetOrganizationBranchesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationDepartmentRow>> GetOrganizationDepartmentsAsync(int tenantId, CancellationToken cancellationToken = default);

    // ── Permissions ───────────────────────────────────────────────────────
    Task<IReadOnlyList<PermissionRowData>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    // ── Modules / registry ────────────────────────────────────────────────
    Task<IReadOnlyList<ModuleRegistryDto>> LoadModuleCatalogAsync(CancellationToken cancellationToken = default);
    Task<ModuleRegistryDto?> LoadModuleByKeyAsync(string codeOrId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetInstalledModuleCodesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<(string Name, string? PlanName)?> GetTenantNameAndPlanAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<string?> GetEnabledModulesJsonAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<TenantModuleLimitsRow> GetTenantModuleLimitsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task EnsureTenantExistsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task ReplaceTenantModulesAsync(int tenantId, IReadOnlyList<string> moduleCodes, CancellationToken cancellationToken = default);

    // ── Roles ─────────────────────────────────────────────────────────────
    Task<IReadOnlyList<RoleSummaryDto>> LoadRoleSummariesAsync(int tenantId, bool visibleOnly = false, CancellationToken cancellationToken = default);
    Task<bool> RoleCodeExistsAsync(int tenantId, string code, CancellationToken cancellationToken = default);
    Task<int> CreateRoleAsync(int tenantId, string name, string code, CancellationToken cancellationToken = default);
    Task<int> CreateRoleForTenantAsync(int tenantId, string name, string code, CancellationToken cancellationToken = default);
    Task<(bool IsSystem, string? Code)?> GetRoleMetaAsync(int tenantId, int roleId, CancellationToken cancellationToken = default);
    Task UpdateRoleForTenantAsync(int tenantId, int roleId, string name, bool isActive, string displayName, string? description, string? category, CancellationToken cancellationToken = default);
    Task<int> CountUsersWithRoleAsync(int roleId, CancellationToken cancellationToken = default);
    Task<int> DeleteRoleAsync(int tenantId, int roleId, CancellationToken cancellationToken = default);
    Task<bool> RoleExistsAsync(int tenantId, int roleId, CancellationToken cancellationToken = default);
    Task ReplaceRolePermissionsAsync(int roleId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default);
    Task ApplyRoleTemplateAsync(int tenantId, string code, string name, RoleRegistrySeed.Entry? seed, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default);

    // ── Tenants ───────────────────────────────────────────────────────────
    Task<IReadOnlyList<TenantListDto>> GetAllTenantsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantListDto>> GetTenantByScopeAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<TenantDetailDto?> GetTenantDetailRowAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetTenantModuleCodesOrderedAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<TenantAdminInfoDto?> GetTenantAdminInfoAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetLegacyModuleKeysAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<int> ResetTenantAdminPasswordAsync(int tenantId, string passwordHash, CancellationToken cancellationToken = default);
    Task<TenantManagementStatsDto> GetTenantManagementStatsGlobalAsync(CancellationToken cancellationToken = default);
    Task<TenantManagementStatsDto> GetTenantManagementStatsScopedAsync(int tenantId, CancellationToken cancellationToken = default);
    Task UpdateTenantAsync(int id, string name, string? subscriptionPlan, bool isActive, int? maxUsers, int? maxVehicles, int? maxDrivers, int? maxBranches, int? maxGpsDevices, IReadOnlyList<string>? moduleCodes, CancellationToken cancellationToken = default);

    // ── User menu / profile ───────────────────────────────────────────────
    Task<(string? DefaultWorkspaceKey, string? HomeRoute, string? RoleCode)?> GetUserWorkspaceProfileAsync(int userId, int tenantId, CancellationToken cancellationToken = default);
    Task<(string? DefaultDashboardKey, string? DefaultWorkspaceKey, string? RoleCode)?> GetUserDashboardProfileAsync(int userId, int tenantId, CancellationToken cancellationToken = default);

    // ── Security settings ─────────────────────────────────────────────────
    Task<TenantSecuritySettingsDto?> GetTenantSecuritySettingsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task UpsertTenantSecuritySettingsAsync(int tenantId, TenantSecuritySettingsDto payload, CancellationToken cancellationToken = default);
    Task<DateTime?> GetUserPasswordChangedAtAsync(int userId, CancellationToken cancellationToken = default);

    // ── License / subscription ────────────────────────────────────────────
    Task<CompanyLicenseDto?> LoadCompanyLicenseAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<string?> ResolvePlanNameAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionPlanDto>> LoadSubscriptionPlansFromDbAsync(CancellationToken cancellationToken = default);
    Task<SubscriptionDetailDto?> GetSubscriptionDetailAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceDto>> GetTenantInvoicesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentDto>> GetTenantBillingHistoryAsync(int tenantId, CancellationToken cancellationToken = default);
    Task ApplySubscriptionActionAsync(int tenantId, SubscriptionAction action, string? planName, decimal? monthlyAmount, string? billingCycle, bool? autoRenew, CancellationToken cancellationToken = default);

    // ── Feature registry ──────────────────────────────────────────────────
    Task<IReadOnlyList<FeatureRegistryQueries.FeatureRow>> LoadVisibleFeaturesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<FeatureRegistryQueries.FeatureRow?> LoadFeatureByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeatureRegistryDto>> LoadCompanyFeaturesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, bool>> LoadTenantFeatureFlagsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task SetCompanyFeaturesAsync(int tenantId, IReadOnlyList<string> enabledKeys, IReadOnlySet<string> toggleableKeys, int? userId, CancellationToken cancellationToken = default);

    // ── Menu builder ──────────────────────────────────────────────────────
    Task<(IReadOnlyList<MenuBuilderQueries.ModuleRow> Modules, IReadOnlyList<MenuBuilderQueries.MenuRow> Menus)> LoadNavTablesAsync(bool activeMenusOnly = false, CancellationToken cancellationToken = default);
    Task<int> UpdateMenuModuleAsync(int id, UpdateMenuModulePayload payload, CancellationToken cancellationToken = default);
    Task<int> UpdateMenuItemAsync(int id, UpdateMenuItemPayload payload, CancellationToken cancellationToken = default);
    Task<int> CreateMenuItemAsync(CreateMenuItemPayload payload, CancellationToken cancellationToken = default);
    Task<int> DeleteMenuItemAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> PlatformModuleExistsAsync(int moduleId, CancellationToken cancellationToken = default);

    // ── Workspace builder ─────────────────────────────────────────────────
    Task<IReadOnlyList<WorkspaceBuilderQueries.WorkspaceRow>> LoadWorkspaceCatalogAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<Dictionary<string, bool>> LoadTenantWorkspaceFlagsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task SetCompanyWorkspacesAsync(int tenantId, IReadOnlySet<string> toggleableKeys, IReadOnlyList<string> enabledKeys, int? userId, CancellationToken cancellationToken = default);
    Task<int> UpdateWorkspaceDefinitionAsync(string key, UpdateWorkspaceDefinitionPayload payload, CancellationToken cancellationToken = default);
    Task<bool> WorkspaceKeyExistsAsync(string key, CancellationToken cancellationToken = default);
    Task CreateWorkspaceDefinitionAsync(string key, CreateWorkspaceDefinitionPayload payload, CancellationToken cancellationToken = default);
    Task<int> DeactivateWorkspaceDefinitionAsync(string key, CancellationToken cancellationToken = default);

    // ── Dashboard builder ─────────────────────────────────────────────────
    Task<bool> DashboardTablesExistAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardBuilderQueries.DashboardRow>> LoadDashboardDefinitionsAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardBuilderQueries.WidgetRow>> LoadDashboardWidgetsAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardBuilderQueries.LayoutJoinRow>> LoadDashboardLayoutAsync(string dashboardKey, bool visibleOnly = false, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> LoadDashboardWidgetCountsAsync(CancellationToken cancellationToken = default);
    Task<ResolvedDashboardDto?> ResolveDashboardForUserAsync(
        string? userDashboardKey,
        string? workspaceDashboardKey,
        string? workspaceKey,
        string? roleCode,
        bool preferMobile,
        HashSet<string> permissions,
        HashSet<string> enabledModules,
        Dictionary<string, bool> featureFlags,
        CancellationToken cancellationToken = default);
    Task<int> UpdateDashboardDefinitionAsync(string key, UpdateDashboardDefinitionPayload payload, CancellationToken cancellationToken = default);
    Task UpdateDashboardLayoutAsync(string key, IReadOnlyList<UpdateDashboardLayoutItemPayload> items, CancellationToken cancellationToken = default);
    Task<bool> DashboardExistsAsync(string key, CancellationToken cancellationToken = default);

    // ── Data scope ────────────────────────────────────────────────────────
    Task<IReadOnlyList<string>> GetBranchLabelsAsync(int tenantId, IReadOnlyList<int> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDepartmentLabelsAsync(int tenantId, IReadOnlyList<int> ids, CancellationToken cancellationToken = default);
    Task<int?> GetUserTenantIdAsync(int userId, CancellationToken cancellationToken = default);

    // ── GPS control ───────────────────────────────────────────────────────
    Task<int> ApproveGpsDeviceCommandAsync(int commandId, bool approve, string? note, string? approvedBy, CancellationToken cancellationToken = default);
}

public sealed record OrganizationBranchRow(
    int Id, int? ParentBranchId, string BranchCode, string Name, string? BranchType,
    string? City, string? Country, bool IsActive, int Status);

public sealed record OrganizationDepartmentRow(
    int Id, int? BranchId, string Name, string? DepartmentHeadName, int StaffCount, bool IsActive);

public sealed record PermissionRowData(
    int Id, string ModuleName, string PermissionCode, string? Description,
    string? DisplayName, string? Category, int SortOrder, bool Visible,
    string? Action, string? ModuleKey);

public sealed record TenantModuleLimitsRow(
    int? MaxUsers, int? MaxVehicles, int? MaxDrivers, int? MaxBranches, int? MaxGpsDevices,
    int UsedUsers, int UsedVehicles, int UsedDrivers, int UsedBranches, int UsedGps);
