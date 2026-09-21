using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Company;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Application.Features.Users;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class CompanyRepository(
    IDbConnectionFactory dbFactory,
    IPlatformRepository platformRepository) : ICompanyRepository
{
    public async Task<CompanyTenantRow?> GetTenantAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyTenantRow>(
            new CommandDefinition("""
                SELECT t.Id, t.Name, t.Slug,
                       COALESCE(b.LogoUrl, t.LogoUrl) AS LogoUrl,
                       COALESCE(b.PrimaryColor, t.PrimaryColor) AS PrimaryColor
                FROM Tenants t
                LEFT JOIN TenantBranding b ON b.TenantId = t.Id
                WHERE t.Id = @TenantId
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<CompanyUserOrgProfileRow?> GetUserOrgProfileAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyUserOrgProfileRow>(
            new CommandDefinition("""
                SELECT u.BranchId,
                       br.Name AS BranchName,
                       u.DepartmentId,
                       d.Name AS DepartmentName,
                       (SELECT TOP 1 r.Code
                        FROM UserRoles ur
                        INNER JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.UserId = u.Id
                        ORDER BY r.Id) AS RoleCode,
                       u.JobTitle, u.EmployeeType, u.Status,
                       u.DefaultWorkspaceKey, u.DefaultDashboardKey, u.HomeRoute,
                       u.Language, u.Theme, u.AvatarUrl, u.EmployeeCode
                FROM Users u
                LEFT JOIN Branches br ON br.Id = u.BranchId
                LEFT JOIN Departments d ON d.Id = u.DepartmentId
                WHERE u.Id = @UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0
                """,
                new { UserId = userId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<CompanyUserOrgFallbackRow?> GetUserOrgFallbackAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyUserOrgFallbackRow>(
            new CommandDefinition("""
                SELECT u.BranchId,
                       br.Name AS BranchName,
                       u.DepartmentId,
                       d.Name AS DepartmentName,
                       (SELECT TOP 1 r.Code
                        FROM UserRoles ur
                        INNER JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.UserId = u.Id
                        ORDER BY r.Id) AS RoleCode
                FROM Users u
                LEFT JOIN Branches br ON br.Id = u.BranchId
                LEFT JOIN Departments d ON d.Id = u.DepartmentId
                WHERE u.Id = @UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0
                """,
                new { UserId = userId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<DateTime?> GetPasswordChangedAtAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT PasswordChangedAt FROM Users WHERE Id = @UserId",
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AssignedRoleDto>> GetAssignedRolesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var rows = await connection.QueryAsync<(
                int RoleId, string Code, string Name, string? DisplayName,
                string? Category, string? RoleType, int? BranchId, int? DepartmentId)>(new CommandDefinition("""
                SELECT r.Id AS RoleId, r.Code, r.Name,
                       COALESCE(r.DisplayName, r.Name) AS DisplayName,
                       r.Category, COALESCE(r.RoleType, CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END) AS RoleType,
                       ur.BranchId, ur.DepartmentId
                FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY COALESCE(r.SortOrder, 0), r.Name
                """, new { UserId = userId }, cancellationToken: cancellationToken));
            return rows.Select(r => new AssignedRoleDto(
                r.RoleId, r.Code, r.Name,
                string.IsNullOrWhiteSpace(r.DisplayName) ? r.Name : r.DisplayName!,
                r.Category, r.RoleType, r.BranchId, r.DepartmentId)).ToList();
        }
        catch
        {
            var rows = await connection.QueryAsync<(
                int RoleId, string Code, string Name, string? DisplayName,
                string? Category, string? RoleType, int? BranchId, int? DepartmentId)>(new CommandDefinition("""
                SELECT r.Id AS RoleId, r.Code, r.Name,
                       r.Name AS DisplayName,
                       CAST(NULL AS NVARCHAR(100)) AS Category,
                       CASE WHEN r.IsSystem = 1 THEN N'System' ELSE N'Custom' END AS RoleType,
                       CAST(NULL AS INT) AS BranchId,
                       CAST(NULL AS INT) AS DepartmentId
                FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY r.Name
                """, new { UserId = userId }, cancellationToken: cancellationToken));
            return rows.Select(r => new AssignedRoleDto(
                r.RoleId, r.Code, r.Name, r.Name, r.Category, r.RoleType, r.BranchId, r.DepartmentId)).ToList();
        }
    }

    public async Task<CompanyDataScopeDto> MapDataScopeAsync(
        DataScopeResult scope,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var branchLabels = Array.Empty<string>();
        var departmentLabels = Array.Empty<string>();

        if (scope.BranchIds.Count > 0)
        {
            branchLabels = (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT Name FROM Branches
                WHERE TenantId = @TenantId AND Id IN @Ids
                ORDER BY Name
                """,
                new { scope.TenantId, Ids = scope.BranchIds.ToArray() },
                cancellationToken: cancellationToken))).ToArray();
        }

        if (scope.DepartmentIds.Count > 0)
        {
            departmentLabels = (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT Name FROM Departments
                WHERE TenantId = @TenantId AND Id IN @Ids
                ORDER BY Name
                """,
                new { scope.TenantId, Ids = scope.DepartmentIds.ToArray() },
                cancellationToken: cancellationToken))).ToArray();
        }

        return new CompanyDataScopeDto(
            Mode: scope.Mode.ToString(),
            IsCompanyWide: scope.IsCompanyWide,
            BranchIds: scope.BranchIds,
            DepartmentIds: scope.DepartmentIds,
            BranchLabels: branchLabels,
            DepartmentLabels: departmentLabels,
            Source: scope.Source,
            HomeBranchId: scope.HomeBranchId,
            HomeDepartmentId: scope.HomeDepartmentId);
    }

    public async Task<IReadOnlyList<CompanyModuleRow>> GetTenantModulesAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            return (await connection.QueryAsync<CompanyModuleRow>(
                new CommandDefinition("""
                    SELECT m.ModuleCode,
                           m.ModuleName AS Name,
                           COALESCE(m.DisplayName, m.ModuleName) AS DisplayName,
                           m.Description, m.Category, m.Version, m.Icon,
                           COALESCE(m.Status, N'Active') AS Status,
                           COALESCE(m.IsMobileSupported, 0) AS IsMobileSupported,
                           COALESCE(m.IsAISupported, 0) AS IsAISupported,
                           COALESCE(m.IsGPSSupported, 0) AS IsGPSSupported
                    FROM TenantModules tm
                    INNER JOIN Modules m ON m.Id = tm.ModuleId
                    WHERE tm.TenantId = @TenantId
                    ORDER BY COALESCE(m.SortOrder, 0), m.ModuleCode
                    """,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            return (await connection.QueryAsync<CompanyModuleRow>(
                new CommandDefinition("""
                    SELECT m.ModuleCode, m.ModuleName AS Name,
                           m.ModuleName AS DisplayName,
                           CAST(NULL AS NVARCHAR(500)) AS Description,
                           CAST(NULL AS NVARCHAR(100)) AS Category,
                           CAST(N'1.0.0' AS NVARCHAR(50)) AS Version,
                           CAST(NULL AS NVARCHAR(100)) AS Icon,
                           CAST(N'Active' AS NVARCHAR(50)) AS Status,
                           CAST(0 AS bit) AS IsMobileSupported,
                           CAST(0 AS bit) AS IsAISupported,
                           CAST(0 AS bit) AS IsGPSSupported
                    FROM TenantModules tm
                    INNER JOIN Modules m ON m.Id = tm.ModuleId
                    WHERE tm.TenantId = @TenantId
                    ORDER BY m.ModuleCode
                    """,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken))).ToList();
        }
    }

    public async Task<IReadOnlyList<CompanyFeatureDto>> GetEnabledCompanyFeaturesAsync(
        int tenantId,
        IReadOnlyList<string> moduleCodes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registry = await platformRepository.LoadCompanyFeaturesAsync(tenantId, cancellationToken);
            var moduleCodeArray = moduleCodes.DefaultIfEmpty("__none__").ToArray();
            return registry
                .Where(f => moduleCodeArray.Contains(f.ModuleKey, StringComparer.OrdinalIgnoreCase))
                .Where(f => f.IsEnabled)
                .Select(FeatureRegistryQueries.ToCompanyFeatureDto)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<CompanyHierarchyCountsDto> GetHierarchyCountsAsync(
        int tenantId,
        int moduleCount,
        int featureCount,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<CompanyHierarchyCountsDto>(
            new CommandDefinition("""
                SELECT
                    (SELECT COUNT(*) FROM Branches WHERE TenantId = @TenantId) AS BranchCount,
                    (SELECT COUNT(*) FROM Departments WHERE TenantId = @TenantId) AS DepartmentCount,
                    (SELECT COUNT(*) FROM Users WHERE TenantId = @TenantId AND IsDeleted = 0) AS UserCount,
                    (SELECT COUNT(*) FROM Drivers WHERE TenantId = @TenantId AND IsDeleted = 0) AS DriverCount,
                    (SELECT COUNT(*) FROM Vehicles WHERE TenantId = @TenantId AND IsDeleted = 0) AS VehicleCount,
                    @ModuleCount AS ModuleCount,
                    @FeatureCount AS FeatureCount
                """,
                new
                {
                    TenantId = tenantId,
                    ModuleCount = moduleCount,
                    FeatureCount = featureCount
                },
                cancellationToken: cancellationToken));
    }

    public async Task<CompanySubscriptionDto?> GetSubscriptionAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var license = await platformRepository.LoadCompanyLicenseAsync(tenantId, cancellationToken);
            if (license is null) return null;

            return new CompanySubscriptionDto(
                license.SubscriptionCode,
                license.PlanName,
                license.PlanDisplayName,
                license.Status,
                license.StartDate,
                license.EndDate,
                license.AutoRenew,
                license.LicensedModules,
                license.MaxUsers,
                license.MaxDrivers,
                license.MaxVehicles,
                license.MaxBranches,
                license.MaxGpsDevices,
                license.StorageQuotaGb,
                license.AICredits,
                license.GPSEnabled,
                license.UsedUsers,
                license.UsedDrivers,
                license.UsedVehicles);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(CompanyNavSummaryDto Nav, IReadOnlyDictionary<string, bool> FeatureFlags)> BuildNavSummaryAsync(
        int tenantId,
        IReadOnlySet<string> permissionCodes,
        IReadOnlyList<string> enabledLegacyKeys,
        IReadOnlyList<string>? workspaceModuleKeys,
        CancellationToken cancellationToken = default)
    {
        var featureFlags = await platformRepository.LoadTenantFeatureFlagsAsync(tenantId, cancellationToken);
        var (navModules, navMenus) = await platformRepository.LoadNavTablesAsync(
            activeMenusOnly: true, cancellationToken);
        var userMenu = MenuBuilderQueries.BuildUserMenu(
            navModules, navMenus, permissionCodes, enabledLegacyKeys, featureFlags, workspaceModuleKeys);
        return (MenuBuilderQueries.ToNavSummary(userMenu), featureFlags);
    }

    public async Task<ResolvedWorkspaceDto?> ResolveWorkspaceAsync(
        int tenantId,
        string? defaultWorkspaceKey,
        string? homeRoute,
        string? roleCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var wsCatalog = await platformRepository.LoadWorkspaceCatalogAsync(activeOnly: true, cancellationToken);
            var wsFlags = await platformRepository.LoadTenantWorkspaceFlagsAsync(tenantId, cancellationToken);
            return WorkspaceBuilderQueries.Resolve(
                wsCatalog,
                wsFlags,
                defaultWorkspaceKey,
                homeRoute,
                roleCode);
        }
        catch
        {
            return null;
        }
    }

    public async Task<CompanyDashboardSummaryDto?> ResolveDashboardAsync(
        string? userDashboardKey,
        string? workspaceDashboardKey,
        string? workspaceKey,
        string? roleCode,
        bool preferMobile,
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> enabledLegacyModules,
        IReadOnlyDictionary<string, bool> featureFlags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var permissionSet = permissions as HashSet<string>
                ?? permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var enabledSet = enabledLegacyModules as HashSet<string>
                ?? enabledLegacyModules.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var flags = featureFlags as Dictionary<string, bool>
                ?? new Dictionary<string, bool>(featureFlags, StringComparer.OrdinalIgnoreCase);

            var resolvedDash = await platformRepository.ResolveDashboardForUserAsync(
                userDashboardKey,
                workspaceDashboardKey,
                workspaceKey,
                roleCode,
                preferMobile,
                permissionSet,
                enabledSet,
                flags,
                cancellationToken);

            if (resolvedDash is null) return null;

            return new CompanyDashboardSummaryDto(
                resolvedDash.Key,
                resolvedDash.DisplayName,
                resolvedDash.WidgetKeys,
                resolvedDash.Source);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<FeatureDefinitionDto>> GetFeatureCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await platformRepository.LoadVisibleFeaturesAsync(cancellationToken: cancellationToken);
        return rows.Select(r => new FeatureDefinitionDto(
            r.FeatureKey,
            r.ModuleKey,
            r.Name,
            r.Description,
            r.SortOrder,
            r.IsActive,
            string.IsNullOrWhiteSpace(r.DisplayName) ? r.Name : r.DisplayName,
            r.Category,
            r.Icon,
            r.Route,
            r.Status,
            r.Visible,
            r.IsMobileSupported,
            r.IsAISupported,
            r.IsGPSSupported,
            r.DocumentationUrl)).ToList();
    }

    public async Task<IReadOnlyList<CompanyFeatureDto>> GetCompanyFeaturesAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var rows = await platformRepository.LoadCompanyFeaturesAsync(tenantId, cancellationToken);
        return rows.Select(FeatureRegistryQueries.ToCompanyFeatureDto).ToList();
    }
}
