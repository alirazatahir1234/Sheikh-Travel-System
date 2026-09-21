using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Company;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for company context / feature catalog reads. SQL lives in Infrastructure.
/// </summary>
public interface ICompanyRepository
{
    Task<CompanyTenantRow?> GetTenantAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<CompanyUserOrgProfileRow?> GetUserOrgProfileAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<CompanyUserOrgFallbackRow?> GetUserOrgFallbackAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetPasswordChangedAtAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignedRoleDto>> GetAssignedRolesAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<CompanyDataScopeDto> MapDataScopeAsync(
        DataScopeResult scope,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyModuleRow>> GetTenantModulesAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyFeatureDto>> GetEnabledCompanyFeaturesAsync(
        int tenantId,
        IReadOnlyList<string> moduleCodes,
        CancellationToken cancellationToken = default);

    Task<CompanyHierarchyCountsDto> GetHierarchyCountsAsync(
        int tenantId,
        int moduleCount,
        int featureCount,
        CancellationToken cancellationToken = default);

    Task<CompanySubscriptionDto?> GetSubscriptionAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads nav tables + feature flags and builds the user nav summary.
    /// </summary>
    Task<(CompanyNavSummaryDto Nav, IReadOnlyDictionary<string, bool> FeatureFlags)> BuildNavSummaryAsync(
        int tenantId,
        IReadOnlySet<string> permissionCodes,
        IReadOnlyList<string> enabledLegacyKeys,
        IReadOnlyList<string>? workspaceModuleKeys,
        CancellationToken cancellationToken = default);

    Task<ResolvedWorkspaceDto?> ResolveWorkspaceAsync(
        int tenantId,
        string? defaultWorkspaceKey,
        string? homeRoute,
        string? roleCode,
        CancellationToken cancellationToken = default);

    Task<CompanyDashboardSummaryDto?> ResolveDashboardAsync(
        string? userDashboardKey,
        string? workspaceDashboardKey,
        string? workspaceKey,
        string? roleCode,
        bool preferMobile,
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> enabledLegacyModules,
        IReadOnlyDictionary<string, bool> featureFlags,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeatureDefinitionDto>> GetFeatureCatalogAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyFeatureDto>> GetCompanyFeaturesAsync(
        int tenantId,
        CancellationToken cancellationToken = default);
}

public sealed record CompanyTenantRow(
    int Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? PrimaryColor);

public sealed record CompanyUserOrgProfileRow(
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    string? RoleCode,
    string? JobTitle,
    string? EmployeeType,
    string? Status,
    string? DefaultWorkspaceKey,
    string? DefaultDashboardKey,
    string? HomeRoute,
    string? Language,
    string? Theme,
    string? AvatarUrl,
    string? EmployeeCode);

public sealed record CompanyUserOrgFallbackRow(
    int? BranchId,
    string? BranchName,
    int? DepartmentId,
    string? DepartmentName,
    string? RoleCode);

public sealed record CompanyModuleRow(
    string ModuleCode,
    string Name,
    string? DisplayName,
    string? Description,
    string? Category,
    string? Version,
    string? Icon,
    string? Status,
    bool IsMobileSupported,
    bool IsAISupported,
    bool IsGPSSupported);
