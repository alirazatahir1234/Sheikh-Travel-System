using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Platform;

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
    CompanySubscriptionDto? Subscription = null);

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
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<GetCompanyContextQuery, ApiResponse<CompanyContextDto>>
{
    public async Task<ApiResponse<CompanyContextDto>> Handle(
        GetCompanyContextQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var company = await connection.QuerySingleOrDefaultAsync<(
            int Id, string Name, string Slug, string? LogoUrl, string? PrimaryColor)>(
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

        if (company.Id == 0)
            return ApiResponse<CompanyContextDto>.FailResponse("Company not found.");

        int? branchId = null;
        string? branchName = null;
        int? departmentId = null;
        string? departmentName = null;
        string? roleCode = currentUser.Role;

        if (currentUser.UserId is int userId)
        {
            var org = await connection.QuerySingleOrDefaultAsync<(
                int? BranchId, string? BranchName, int? DepartmentId, string? DepartmentName, string? RoleCode)>(
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

            branchId = org.BranchId;
            branchName = org.BranchName;
            departmentId = org.DepartmentId;
            departmentName = org.DepartmentName;
            if (!string.IsNullOrWhiteSpace(org.RoleCode))
                roleCode = org.RoleCode;
        }

        List<(string ModuleCode, string Name, string? DisplayName, string? Description, string? Category,
            string? Version, string? Icon, string? Status, bool IsMobileSupported, bool IsAISupported, bool IsGPSSupported)> moduleCodes;

        try
        {
            moduleCodes = (await connection.QueryAsync<(
                string ModuleCode, string Name, string? DisplayName, string? Description, string? Category,
                string? Version, string? Icon, string? Status, bool IsMobileSupported, bool IsAISupported, bool IsGPSSupported)>(
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
            moduleCodes = (await connection.QueryAsync<(
                string ModuleCode, string Name, string? DisplayName, string? Description, string? Category,
                string? Version, string? Icon, string? Status, bool IsMobileSupported, bool IsAISupported, bool IsGPSSupported)>(
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
                    return (
                        ModuleCode: m.Code,
                        Name: m.Name,
                        DisplayName: (string?)(seed?.DisplayName ?? m.Name),
                        Description: seed?.Description,
                        Category: seed?.Category,
                        Version: (string?)(seed?.Version ?? "1.0.0"),
                        Icon: seed?.Icon,
                        Status: (string?)(seed?.Status ?? "Active"),
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

        var moduleCodeArray = moduleCodes.Select(m => m.ModuleCode).DefaultIfEmpty("__none__").ToArray();
        var features = new List<CompanyFeatureDto>();
        try
        {
            var registry = await FeatureRegistryQueries.LoadCompanyFeaturesAsync(
                connection, tenantId, cancellationToken);
            features = registry
                .Where(f => moduleCodeArray.Contains(f.ModuleKey, StringComparer.OrdinalIgnoreCase))
                .Where(f => f.IsEnabled)
                .Select(FeatureRegistryQueries.ToCompanyFeatureDto)
                .ToList();
        }
        catch
        {
            // Feature registry migration may not have applied yet.
            features = [];
        }

        var counts = await connection.QuerySingleAsync<CompanyHierarchyCountsDto>(
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
                    ModuleCount = moduleCodes.Count,
                    FeatureCount = features.Count
                },
                cancellationToken: cancellationToken));

        var workspaceHint = ResolveWorkspaceHint(roleCode);

        CompanySubscriptionDto? subscription = null;
        try
        {
            var license = await LicenseQueries.LoadCompanyLicenseAsync(connection, tenantId, cancellationToken);
            if (license is not null)
            {
                subscription = new CompanySubscriptionDto(
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
        }
        catch
        {
            // License columns / catalog may not exist yet.
        }

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
            Subscription: subscription);

        return ApiResponse<CompanyContextDto>.SuccessResponse(dto);
    }

    private static string ResolveWorkspaceHint(string? roleCode)
    {
        var code = (roleCode ?? string.Empty).ToUpperInvariant();
        return code switch
        {
            "SUPER_ADMIN" => "platform",
            "TENANT_ADMIN" => "company",
            "FLEET_MANAGER" => "fleet",
            "DRIVER_MANAGER" => "drivers",
            "DISPATCHER" => "trips",
            "ACCOUNTANT" => "finance",
            "DRIVER" => "driver",
            _ => "home"
        };
    }
}

public class GetFeatureCatalogQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetFeatureCatalogQuery, ApiResponse<IReadOnlyList<FeatureDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<FeatureDefinitionDto>>> Handle(
        GetFeatureCatalogQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await FeatureRegistryQueries.LoadVisibleAsync(connection, cancellationToken);
        var dtos = rows.Select(r => new FeatureDefinitionDto(
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

        return ApiResponse<IReadOnlyList<FeatureDefinitionDto>>.SuccessResponse(dtos);
    }
}

public class GetCompanyFeaturesQueryHandler(
    IDbConnectionFactory dbFactory,
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

        using var connection = dbFactory.CreateConnection();
        var rows = await FeatureRegistryQueries.LoadCompanyFeaturesAsync(
            connection, tenantId, cancellationToken);
        var dtos = rows.Select(FeatureRegistryQueries.ToCompanyFeatureDto).ToList();
        return ApiResponse<IReadOnlyList<CompanyFeatureDto>>.SuccessResponse(dtos);
    }
}
