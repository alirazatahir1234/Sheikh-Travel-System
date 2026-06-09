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
    IReadOnlyList<string> ModuleCodes,
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
    string? Location);

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
