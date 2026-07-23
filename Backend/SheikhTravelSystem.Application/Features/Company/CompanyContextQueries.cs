using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Company;

public record CompanyFeatureDto(
    string FeatureKey,
    string ModuleKey,
    string Name,
    string? Description,
    bool IsEnabled,
    int SortOrder);

public record CompanyModuleDto(string ModuleCode, string Name);

public record CompanyHierarchyCountsDto(
    int BranchCount,
    int DepartmentCount,
    int UserCount,
    int DriverCount,
    int VehicleCount,
    int ModuleCount,
    int FeatureCount);

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
    string? RoleCode);

public record GetCompanyContextQuery : IRequest<ApiResponse<CompanyContextDto>>;

public record FeatureDefinitionDto(
    string FeatureKey,
    string ModuleKey,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive);

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

        var moduleCodes = (await connection.QueryAsync<(string ModuleCode, string Name)>(
            new CommandDefinition("""
                SELECT m.ModuleCode, m.ModuleName AS Name
                FROM TenantModules tm
                INNER JOIN Modules m ON m.Id = tm.ModuleId
                WHERE tm.TenantId = @TenantId
                ORDER BY m.ModuleCode
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        IReadOnlyList<string> enabledLegacyKeys;
        if (moduleCodes.Count == 0)
        {
            enabledLegacyKeys = await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken);
            moduleCodes = TenantModuleCatalog.All
                .Where(m => enabledLegacyKeys.Any(k =>
                    m.LegacyKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
                .Select(m => (m.Code, m.Name))
                .ToList();
        }
        else
        {
            enabledLegacyKeys = TenantModuleCatalog.LegacyKeysFromCodes(moduleCodes.Select(m => m.ModuleCode));
        }

        var moduleCodeArray = moduleCodes.Select(m => m.ModuleCode).DefaultIfEmpty("__none__").ToArray();
        var features = new List<CompanyFeatureDto>();
        try
        {
            features = (await connection.QueryAsync<CompanyFeatureDto>(
                new CommandDefinition("""
                    SELECT fd.FeatureKey, fd.ModuleKey, fd.Name, fd.Description,
                           CAST(COALESCE(tf.IsEnabled, 1) AS bit) AS IsEnabled,
                           fd.SortOrder
                    FROM FeatureDefinitions fd
                    LEFT JOIN TenantFeatures tf
                        ON tf.FeatureKey = fd.FeatureKey AND tf.TenantId = @TenantId
                    WHERE fd.IsActive = 1
                      AND fd.ModuleKey IN @ModuleCodes
                    ORDER BY fd.SortOrder, fd.FeatureKey
                    """,
                    new { TenantId = tenantId, ModuleCodes = moduleCodeArray },
                    cancellationToken: cancellationToken))).ToList();
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
            Modules: moduleCodes.Select(m => new CompanyModuleDto(m.ModuleCode, m.Name)).ToList(),
            Features: features,
            Hierarchy: counts,
            WorkspaceHint: workspaceHint,
            RoleCode: roleCode);

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
        var rows = (await connection.QueryAsync<FeatureDefinitionDto>(new CommandDefinition("""
            SELECT FeatureKey, ModuleKey, Name, Description, SortOrder, IsActive
            FROM FeatureDefinitions
            WHERE IsActive = 1
            ORDER BY SortOrder, FeatureKey
            """, cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<FeatureDefinitionDto>>.SuccessResponse(rows);
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
        var rows = (await connection.QueryAsync<CompanyFeatureDto>(new CommandDefinition("""
            SELECT fd.FeatureKey, fd.ModuleKey, fd.Name, fd.Description,
                   CAST(COALESCE(tf.IsEnabled, 1) AS bit) AS IsEnabled,
                   fd.SortOrder
            FROM FeatureDefinitions fd
            LEFT JOIN TenantFeatures tf
                ON tf.FeatureKey = fd.FeatureKey AND tf.TenantId = @TenantId
            WHERE fd.IsActive = 1
            ORDER BY fd.SortOrder, fd.FeatureKey
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<CompanyFeatureDto>>.SuccessResponse(rows);
    }
}
