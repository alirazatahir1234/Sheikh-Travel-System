using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Platform;

public record BranchDto(
    int Id,
    int TenantId,
    int? ParentBranchId,
    string BranchCode,
    string Name,
    string? BranchType,
    int? BranchManagerUserId,
    string? BranchManagerName,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? TimeZone,
    string? CurrencyCode,
    int Status,
    bool IsGpsEnabled,
    bool IsActive);

public record DepartmentDto(
    int Id,
    int TenantId,
    int? BranchId,
    string Name,
    int? DepartmentHeadUserId,
    string? DepartmentHeadName,
    bool IsActive,
    DateTime CreatedAt,
    int StaffCount);

public record DepartmentUpsertPayload(string Name, int? DepartmentHeadUserId);
public record RoleDto(
    int Id,
    int TenantId,
    string Name,
    string Code,
    bool IsSystem,
    bool IsActive,
    IReadOnlyList<string> Permissions,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? RoleType = null,
    int SortOrder = 0,
    bool Visible = true);
public record PermissionDto(
    int Id,
    string ModuleName,
    string PermissionCode,
    string? Description,
    string? DisplayName = null,
    string? Category = null,
    int SortOrder = 0,
    bool Visible = true,
    string? Action = null,
    string? ModuleKey = null);
public record TenantListDto(
    int Id,
    string Name,
    string Slug,
    string? Code,
    string? TenantType,
    string? Country,
    string? SubscriptionPlan,
    bool IsActive,
    DateTime CreatedAt,
    int BranchCount,
    int DepartmentCount,
    int RoleCount,
    string? Location,
    int ActiveUserCount,
    int? MaxUsers,
    int ActiveVehicleCount,
    int? MaxVehicles,
    string? ModuleCodes,
    DateTime? SubscriptionEndDate,
    string? SubscriptionStatus)
{
    /// <summary>Company alias for product language (persistence remains Tenant).</summary>
    public int CompanyId => Id;
    public string CompanyName => Name;
}

public record TenantManagementStatsDto(
    int ActiveTenants,
    int ActiveUsers,
    int ActiveVehicles,
    int ExpiringPlans,
    decimal MonthlyRevenue,
    int TenantsAddedThisMonth);

public record GetTenantManagementStatsQuery : IRequest<ApiResponse<TenantManagementStatsDto>>;
public record MenuModuleDto(
    string Id,
    string Label,
    string Icon,
    bool Collapsible,
    int SortOrder,
    IReadOnlyList<MenuItemDto> Items,
    string? DisplayName = null,
    string? Description = null,
    bool Visible = true);
public record MenuItemDto(
    string Id,
    string Label,
    string Icon,
    string Route,
    string? PermissionCode,
    int SortOrder,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? FeatureKey = null,
    string? ModuleKey = null,
    bool IsMobileSupported = false,
    bool Visible = true);

public record MenuCatalogModuleDto(
    int Id,
    string ModuleKey,
    string Name,
    string DisplayName,
    string? Description,
    string? Icon,
    int SortOrder,
    bool IsCollapsible,
    bool Visible,
    IReadOnlyList<MenuCatalogItemDto> Items);

public record MenuCatalogItemDto(
    int Id,
    int ModuleId,
    string Name,
    string DisplayName,
    string? Description,
    string? Category,
    string? Route,
    string? Icon,
    string? PermissionCode,
    int SortOrder,
    bool IsActive,
    bool Visible,
    string? FeatureKey,
    string? ModuleKey,
    bool IsMobileSupported,
    int? ParentId = null);

public record MenuCatalogDto(IReadOnlyList<MenuCatalogModuleDto> Modules);

public record UpdateMenuModulePayload(
    string? DisplayName,
    string? Icon,
    int SortOrder,
    bool Visible,
    bool IsCollapsible);

public record UpdateMenuItemPayload(
    string? DisplayName,
    string? Description,
    string? Category,
    string? Route,
    string? Icon,
    string? PermissionCode,
    int SortOrder,
    bool IsActive,
    bool Visible,
    string? FeatureKey,
    string? ModuleKey,
    bool IsMobileSupported);

public record CreateMenuItemPayload(
    int ModuleId,
    string Name,
    string? DisplayName,
    string? Description,
    string? Category,
    string? Route,
    string? Icon,
    string? PermissionCode,
    int SortOrder,
    bool Visible = true,
    string? FeatureKey = null,
    string? ModuleKey = null,
    bool IsMobileSupported = false);

public record CompanyNavSummaryDto(
    int ModuleCount,
    int ItemCount,
    IReadOnlyList<string> TopModuleLabels,
    IReadOnlyList<string> MobileItemLabels);

public record BranchUpsertPayload(
    string BranchCode,
    string Name,
    string? BranchType,
    int? ParentBranchId,
    int? BranchManagerUserId,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? TimeZone,
    string? CurrencyCode,
    int Status,
    bool IsGpsEnabled);

public record GetBranchesQuery : IRequest<ApiResponse<IReadOnlyList<BranchDto>>>;
public record GetBranchByIdQuery(int Id) : IRequest<ApiResponse<BranchDto>>;
public record CreateBranchCommand(BranchUpsertPayload Payload) : IRequest<ApiResponse<int>>;
public record UpdateBranchCommand(int Id, BranchUpsertPayload Payload) : IRequest<ApiResponse<bool>>;
public record DeleteBranchCommand(int Id) : IRequest<ApiResponse<bool>>;

public record GetDepartmentsQuery : IRequest<ApiResponse<IReadOnlyList<DepartmentDto>>>;
public record CreateDepartmentCommand(DepartmentUpsertPayload Payload) : IRequest<ApiResponse<int>>;
public record UpdateDepartmentCommand(int Id, DepartmentUpsertPayload Payload, bool IsActive) : IRequest<ApiResponse<bool>>;
public record DeleteDepartmentCommand(int Id) : IRequest<ApiResponse<bool>>;

public record GetRolesQuery : IRequest<ApiResponse<IReadOnlyList<RoleDto>>>;
public record CreateRoleCommand(string Name, string Code) : IRequest<ApiResponse<int>>;
public record UpdateRolePermissionsCommand(int RoleId, IReadOnlyList<string> PermissionCodes) : IRequest<ApiResponse<bool>>;

public record GetPermissionsQuery(
    string? Category = null,
    string? ModuleKey = null,
    string? Action = null,
    bool? Visible = null) : IRequest<ApiResponse<IReadOnlyList<PermissionDto>>>;

public record GetEffectivePermissionsQuery : IRequest<ApiResponse<IReadOnlyList<EffectivePermissionDto>>>;
public record GetUserPermissionsQuery(int UserId) : IRequest<ApiResponse<IReadOnlyList<EffectivePermissionDto>>>;

public record TenantModuleDefinitionDto(
    string Code,
    string Name,
    IReadOnlyList<string> LegacyKeys,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? Version = null,
    string? Icon = null,
    string? Route = null,
    int SortOrder = 0,
    IReadOnlyList<string>? Dependencies = null,
    bool Visible = true,
    bool IsMobileSupported = false,
    bool IsAISupported = false,
    bool IsGPSSupported = false,
    string Status = "Active",
    string? DocumentationUrl = null,
    bool IsEnableable = true,
    int? Id = null);

public record ModuleRegistryDto(
    string Code,
    string Name,
    string DisplayName,
    string? Description,
    string? Category,
    string Version,
    string? Icon,
    string? Route,
    int SortOrder,
    IReadOnlyList<string> Dependencies,
    bool Visible,
    bool IsMobileSupported,
    bool IsAISupported,
    bool IsGPSSupported,
    string Status,
    string? DocumentationUrl,
    IReadOnlyList<string> LegacyKeys,
    bool IsEnableable,
    int? Id = null,
    bool IsInstalled = false,
    bool IsLicensed = false);

public record GetTenantModulesQuery : IRequest<ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>>;
public record GetModuleCatalogQuery : IRequest<ApiResponse<IReadOnlyList<ModuleRegistryDto>>>;
public record GetCompanyModulesQuery : IRequest<ApiResponse<IReadOnlyList<ModuleRegistryDto>>>;
public record GetModuleByKeyQuery(string CodeOrId) : IRequest<ApiResponse<ModuleRegistryDto>>;

public record GetTenantsQuery : IRequest<ApiResponse<IReadOnlyList<TenantListDto>>>;

public record TenantAdminInfoDto(
    int Id,
    string FullName,
    string Email,
    string? Phone,
    bool IsActive,
    string Status);

public record ResetTenantAdminPasswordCommand(int TenantId, string NewPassword)
    : IRequest<ApiResponse<bool>>;

public record TenantDetailDto(
    int Id,
    string Name,
    string Slug,
    string? Code,
    string? TenantType,
    string? IndustryType,
    string StorageModel,
    string Status,
    bool IsActive,
    string? DataRegion,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? SubscriptionPlan,
    int? MaxUsers,
    int? MaxVehicles,
    int? MaxDrivers,
    int? MaxBranches,
    int? MaxGpsDevices,
    string? LogoUrl,
    string? PrimaryColor,
    string? Website,
    string? SupportEmail,
    string? Country,
    string? CurrencyCode,
    string? TimeZone,
    int BranchCount,
    int DepartmentCount,
    int RoleCount,
    string? Location)
{
    public IReadOnlyList<string> ModuleCodes { get; init; } = [];
    public TenantAdminInfoDto? AdminInfo { get; init; }

    /// <summary>Company alias for product language (persistence remains Tenant).</summary>
    public int CompanyId => Id;
    public string CompanyName => Name;
}

public record GetTenantByIdQuery(int Id) : IRequest<ApiResponse<TenantDetailDto>>;

public record UpdateTenantCommand(
    int Id,
    string Name,
    string? SubscriptionPlan,
    bool IsActive,
    IReadOnlyList<string>? EnabledModules,
    IReadOnlyList<string>? ModuleCodes,
    int? MaxUsers,
    int? MaxVehicles,
    int? MaxDrivers,
    int? MaxBranches,
    int? MaxGpsDevices)
    : IRequest<ApiResponse<bool>>;

public record GetUserMenuQuery : IRequest<ApiResponse<IReadOnlyList<MenuModuleDto>>>;
public record GetMenuCatalogQuery : IRequest<ApiResponse<MenuCatalogDto>>;
public record UpdateMenuModuleCommand(int Id, UpdateMenuModulePayload Payload) : IRequest<ApiResponse<bool>>;
public record UpdateMenuItemCommand(int Id, UpdateMenuItemPayload Payload) : IRequest<ApiResponse<bool>>;
public record CreateMenuItemCommand(CreateMenuItemPayload Payload) : IRequest<ApiResponse<int>>;
public record DeleteMenuItemCommand(int Id) : IRequest<ApiResponse<bool>>;

public record UpdateDepartmentRequest(DepartmentUpsertPayload Payload, bool IsActive);

// Organization Designer DTOs
public record OrganizationTreeDto(
    int TenantId,
    string TenantName,
    IReadOnlyList<OrganizationBranchDto> Branches,
    IReadOnlyList<OrganizationDepartmentDto> UnassignedDepartments);

public record OrganizationBranchDto(
    int Id,
    int? ParentBranchId,
    string BranchCode,
    string Name,
    string? BranchType,
    string? City,
    string? Country,
    bool IsActive,
    int Status,
    IReadOnlyList<OrganizationDepartmentDto> Departments);

public record OrganizationDepartmentDto(
    int Id,
    int? BranchId,
    string Name,
    string? DepartmentHeadName,
    int StaffCount,
    bool IsActive);

public record DepartmentUpsertWithBranchPayload(string Name, int? DepartmentHeadUserId, int? BranchId);

// Tenant-scoped queries for Organization Designer
public record GetOrganizationTreeQuery(int TenantId) : IRequest<ApiResponse<OrganizationTreeDto>>;

// Tenant-scoped branch commands
public record GetBranchesForTenantQuery(int TenantId) : IRequest<ApiResponse<IReadOnlyList<BranchDto>>>;
public record CreateBranchForTenantCommand(int TenantId, BranchUpsertPayload Payload) : IRequest<ApiResponse<int>>;
public record UpdateBranchForTenantCommand(int TenantId, int BranchId, BranchUpsertPayload Payload) : IRequest<ApiResponse<bool>>;
public record DeleteBranchForTenantCommand(int TenantId, int BranchId) : IRequest<ApiResponse<bool>>;

// Tenant-scoped department commands
public record GetDepartmentsForTenantQuery(int TenantId) : IRequest<ApiResponse<IReadOnlyList<DepartmentDto>>>;
public record CreateDepartmentForTenantCommand(int TenantId, DepartmentUpsertWithBranchPayload Payload) : IRequest<ApiResponse<int>>;
public record UpdateDepartmentForTenantCommand(int TenantId, int DepartmentId, DepartmentUpsertWithBranchPayload Payload, bool IsActive) : IRequest<ApiResponse<bool>>;
public record DeleteDepartmentForTenantCommand(int TenantId, int DepartmentId) : IRequest<ApiResponse<bool>>;
public record MoveDepartmentCommand(int TenantId, int DepartmentId, int? NewBranchId) : IRequest<ApiResponse<bool>>;

// Access Control (Sprint 2)
public record RoleSummaryDto(
    int Id,
    int TenantId,
    string Name,
    string Code,
    bool IsSystem,
    bool IsActive,
    int UserCount,
    int PermissionCount,
    IReadOnlyList<string> Permissions,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? RoleType = null,
    int SortOrder = 0,
    bool Visible = true);

public record TenantSecuritySettingsDto(
    bool IsMfaRequired,
    int? PasswordExpiryDays,
    int? SessionTimeoutMinutes,
    bool IsGdprEnabled,
    bool IsAuditLoggingEnabled,
    bool IsVatEnabled);

public record RoleTemplateDto(
    string Code,
    string Name,
    int PermissionCount,
    IReadOnlyList<string> Permissions,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null);

public record GetRolesForTenantQuery(int TenantId) : IRequest<ApiResponse<IReadOnlyList<RoleSummaryDto>>>;
public record GetCompanyRolesQuery(int? TenantId = null) : IRequest<ApiResponse<IReadOnlyList<RoleSummaryDto>>>;
public record CreateRoleForTenantCommand(int TenantId, string Name, string Code) : IRequest<ApiResponse<int>>;
public record UpdateRoleForTenantCommand(
    int TenantId,
    int RoleId,
    string Name,
    bool IsActive,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null) : IRequest<ApiResponse<bool>>;
public record DeleteRoleForTenantCommand(int TenantId, int RoleId) : IRequest<ApiResponse<bool>>;
public record UpdateRolePermissionsForTenantCommand(int TenantId, int RoleId, IReadOnlyList<string> PermissionCodes) : IRequest<ApiResponse<bool>>;

public record GetTenantSecuritySettingsQuery(int TenantId) : IRequest<ApiResponse<TenantSecuritySettingsDto>>;
public record UpdateTenantSecuritySettingsCommand(int TenantId, TenantSecuritySettingsDto Payload) : IRequest<ApiResponse<bool>>;

public record GetRoleTemplatesQuery : IRequest<ApiResponse<IReadOnlyList<RoleTemplateDto>>>;
public record ApplyRoleTemplateCommand(int TenantId, string RoleCode) : IRequest<ApiResponse<bool>>;

// Module Management / Module Registry (Stage 3)
public record ModuleStatusDto(
    string Code,
    string Name,
    bool IsEnabled,
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? Version = null,
    string? Icon = null,
    string? Route = null,
    int SortOrder = 0,
    IReadOnlyList<string>? Dependencies = null,
    bool Visible = true,
    bool IsMobileSupported = false,
    bool IsAISupported = false,
    bool IsGPSSupported = false,
    string Status = "Active",
    string? DocumentationUrl = null,
    bool IsInstalled = false,
    bool IsLicensed = false,
    bool CanToggle = true);

public record LicenseLimitDto(
    string Resource,
    int Used,
    int? Limit);

public record TenantModuleOverviewDto(
    int TenantId,
    string TenantName,
    string? PlanName,
    IReadOnlyList<ModuleStatusDto> Modules,
    IReadOnlyList<LicenseLimitDto> LicenseLimits);

public record GetTenantModuleOverviewQuery(int TenantId) : IRequest<ApiResponse<TenantModuleOverviewDto>>;
public record SetTenantModulesCommand(int TenantId, IReadOnlyList<string> ModuleCodes) : IRequest<ApiResponse<bool>>;

// Subscription Management / License (Stage 4)
public record SubscriptionDetailDto
{
    public int TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string? PlanName { get; init; }
    public string? SubscriptionCode { get; init; }
    public string Status { get; init; } = "Active";
    public string BillingCycle { get; init; } = "Monthly";
    public decimal? MonthlyAmount { get; init; }
    public string CurrencyCode { get; init; } = "PKR";
    public bool AutoRenew { get; init; }
    public DateTime? SubscriptionStartDate { get; init; }
    public DateTime? SubscriptionEndDate { get; init; }
    public DateTime? TrialEndDate { get; init; }
    public int? MaxUsers { get; init; }
    public int? MaxVehicles { get; init; }
    public int? MaxDrivers { get; init; }
    public int? MaxBranches { get; init; }
    public int? MaxGpsDevices { get; init; }
    public int? StorageQuotaGb { get; init; }
    public int? AICredits { get; init; }
    public bool GPSEnabled { get; init; } = true;
    public IReadOnlyList<string> LicensedModuleCodes { get; init; } = [];
}

public record SubscriptionPlanDto(
    string SubscriptionCode,
    string DisplayName,
    string? Description,
    string PlanType,
    string Status,
    int SortOrder,
    int? DurationMonths,
    bool IsDefault,
    bool Visible,
    string? DocumentationUrl,
    IReadOnlyList<string> DefaultModuleCodes,
    int? MaxUsers,
    int? MaxVehicles,
    int? MaxDrivers,
    int? MaxBranches,
    int? MaxGpsDevices,
    int? StorageQuotaGb,
    int? AICredits,
    bool GPSEnabled);

public record CompanyLicenseDto(
    int CompanyId,
    int TenantId,
    string CompanyName,
    string? SubscriptionCode,
    string? PlanName,
    string? PlanDisplayName,
    string Status,
    DateTime? StartDate,
    DateTime? EndDate,
    bool AutoRenew,
    IReadOnlyList<string> LicensedModules,
    IReadOnlyList<string> InstalledModules,
    int? MaxUsers,
    int? MaxDrivers,
    int? MaxVehicles,
    int? MaxBranches,
    int? MaxGpsDevices,
    int? StorageQuotaGb,
    int? AICredits,
    bool GPSEnabled,
    int UsedUsers,
    int UsedDrivers,
    int UsedVehicles,
    int UsedBranches,
    int UsedGpsDevices);

public record LicenseSummaryDto(
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
    int? StorageQuotaGb,
    int? AICredits,
    bool GPSEnabled);

public record GetSubscriptionCatalogQuery : IRequest<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>;
public record GetCompanyLicenseQuery(int? TenantId = null) : IRequest<ApiResponse<CompanyLicenseDto>>;
public record GetLicenseSummaryQuery(int? TenantId = null) : IRequest<ApiResponse<LicenseSummaryDto>>;

public record InvoiceDto(
    int Id,
    string InvoiceNumber,
    string? PlanName,
    decimal Amount,
    string CurrencyCode,
    string Status,
    DateTime IssuedDate,
    DateTime? DueDate,
    DateTime? PaidDate);

public record PaymentDto(
    int Id,
    int? InvoiceId,
    decimal Amount,
    string CurrencyCode,
    string? PaymentMethod,
    string Status,
    string? Reference,
    DateTime PaidAt);

public record SubscriptionOverviewDto(
    SubscriptionDetailDto Subscription,
    IReadOnlyList<InvoiceDto> Invoices,
    IReadOnlyList<PaymentDto> Payments,
    CompanyLicenseDto? License = null);

public record GetSubscriptionOverviewQuery(int TenantId) : IRequest<ApiResponse<SubscriptionOverviewDto>>;

public enum SubscriptionAction { Upgrade, Renew, Suspend, Cancel, Reactivate }

public record UpdateSubscriptionCommand(
    int TenantId,
    SubscriptionAction Action,
    string? PlanName,
    decimal? MonthlyAmount,
    bool? AutoRenew,
    string? BillingCycle) : IRequest<ApiResponse<bool>>;
