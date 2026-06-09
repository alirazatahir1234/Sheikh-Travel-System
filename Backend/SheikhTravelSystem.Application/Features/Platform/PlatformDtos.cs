using MediatR;
using SheikhTravelSystem.Application.Common;

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
public record RoleDto(int Id, int TenantId, string Name, string Code, bool IsSystem, bool IsActive, IReadOnlyList<string> Permissions);
public record PermissionDto(int Id, string ModuleName, string PermissionCode, string? Description);
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
    string? SubscriptionStatus);

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
    IReadOnlyList<MenuItemDto> Items);
public record MenuItemDto(
    string Id,
    string Label,
    string Icon,
    string Route,
    string? PermissionCode,
    int SortOrder);

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

public record GetPermissionsQuery : IRequest<ApiResponse<IReadOnlyList<PermissionDto>>>;

public record TenantModuleDefinitionDto(string Code, string Name, IReadOnlyList<string> LegacyKeys);
public record GetTenantModulesQuery : IRequest<ApiResponse<IReadOnlyList<TenantModuleDefinitionDto>>>;

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
    IReadOnlyList<string> Permissions);

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
    IReadOnlyList<string> Permissions);

public record GetRolesForTenantQuery(int TenantId) : IRequest<ApiResponse<IReadOnlyList<RoleSummaryDto>>>;
public record CreateRoleForTenantCommand(int TenantId, string Name, string Code) : IRequest<ApiResponse<int>>;
public record UpdateRoleForTenantCommand(int TenantId, int RoleId, string Name, bool IsActive) : IRequest<ApiResponse<bool>>;
public record DeleteRoleForTenantCommand(int TenantId, int RoleId) : IRequest<ApiResponse<bool>>;
public record UpdateRolePermissionsForTenantCommand(int TenantId, int RoleId, IReadOnlyList<string> PermissionCodes) : IRequest<ApiResponse<bool>>;

public record GetTenantSecuritySettingsQuery(int TenantId) : IRequest<ApiResponse<TenantSecuritySettingsDto>>;
public record UpdateTenantSecuritySettingsCommand(int TenantId, TenantSecuritySettingsDto Payload) : IRequest<ApiResponse<bool>>;

public record GetRoleTemplatesQuery : IRequest<ApiResponse<IReadOnlyList<RoleTemplateDto>>>;
public record ApplyRoleTemplateCommand(int TenantId, string RoleCode) : IRequest<ApiResponse<bool>>;

// Module Management (Sprint 3)
public record ModuleStatusDto(
    string Code,
    string Name,
    bool IsEnabled);

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

// Subscription Management (Sprint 4)
public record SubscriptionDetailDto(
    int TenantId,
    string TenantName,
    string? PlanName,
    string Status,
    string BillingCycle,
    decimal? MonthlyAmount,
    string CurrencyCode,
    bool AutoRenew,
    DateTime? SubscriptionStartDate,
    DateTime? SubscriptionEndDate,
    DateTime? TrialEndDate,
    int? MaxUsers,
    int? MaxVehicles,
    int? MaxDrivers,
    int? MaxBranches,
    int? MaxGpsDevices);

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
    IReadOnlyList<PaymentDto> Payments);

public record GetSubscriptionOverviewQuery(int TenantId) : IRequest<ApiResponse<SubscriptionOverviewDto>>;

public enum SubscriptionAction { Upgrade, Renew, Suspend, Cancel, Reactivate }

public record UpdateSubscriptionCommand(
    int TenantId,
    SubscriptionAction Action,
    string? PlanName,
    decimal? MonthlyAmount,
    bool? AutoRenew,
    string? BillingCycle) : IRequest<ApiResponse<bool>>;
