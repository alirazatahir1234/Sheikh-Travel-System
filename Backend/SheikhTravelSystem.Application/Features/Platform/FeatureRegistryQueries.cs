using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Company;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Shared mapping for Stage 5 Feature Registry reads. SQL lives in IPlatformRepository.</summary>
public static class FeatureRegistryQueries
{
    public const string SelectSql = """
        SELECT fd.FeatureKey, fd.ModuleKey,
               fd.Name,
               COALESCE(fd.DisplayName, fd.Name) AS DisplayName,
               fd.Description, fd.Category, fd.Icon, fd.Route,
               COALESCE(fd.SortOrder, 0) AS SortOrder,
               COALESCE(fd.Visible, 1) AS Visible,
               COALESCE(fd.Status, N'Active') AS Status,
               COALESCE(fd.IsMobileSupported, 0) AS IsMobileSupported,
               COALESCE(fd.IsAISupported, 0) AS IsAISupported,
               COALESCE(fd.IsGPSSupported, 0) AS IsGPSSupported,
               fd.DocumentationUrl,
               CAST(COALESCE(fd.IsActive, 1) AS bit) AS IsActive
        FROM FeatureDefinitions fd
        """;

    public sealed class FeatureRow
    {
        public string FeatureKey { get; init; } = "";
        public string ModuleKey { get; init; } = "";
        public string Name { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string? Description { get; init; }
        public string? Category { get; init; }
        public string? Icon { get; init; }
        public string? Route { get; init; }
        public int SortOrder { get; init; }
        public bool Visible { get; init; } = true;
        public string Status { get; init; } = "Active";
        public bool IsMobileSupported { get; init; }
        public bool IsAISupported { get; init; }
        public bool IsGPSSupported { get; init; }
        public string? DocumentationUrl { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public static FeatureRegistryDto ToRegistryDto(FeatureRow row, bool isEnabled = false, bool moduleInstalled = false)
    {
        var toggleable = FeatureRegistrySeed.IsToggleable(row.Status) && row.Visible && row.IsActive;
        return new FeatureRegistryDto(
            FeatureKey: row.FeatureKey,
            ModuleKey: row.ModuleKey,
            ModuleCode: row.ModuleKey,
            Name: row.Name,
            DisplayName: string.IsNullOrWhiteSpace(row.DisplayName) ? row.Name : row.DisplayName,
            Description: row.Description,
            Category: row.Category,
            Icon: row.Icon,
            Route: row.Route,
            SortOrder: row.SortOrder,
            Visible: row.Visible,
            Status: row.Status,
            IsMobileSupported: row.IsMobileSupported,
            IsAISupported: row.IsAISupported,
            IsGPSSupported: row.IsGPSSupported,
            DocumentationUrl: row.DocumentationUrl,
            IsActive: row.IsActive,
            IsEnabled: isEnabled,
            IsModuleInstalled: moduleInstalled,
            CanToggle: toggleable && moduleInstalled);
    }

    public static FeatureRegistryDto FromSeed(FeatureRegistrySeed.Entry entry, bool isEnabled = false, bool moduleInstalled = false)
    {
        var toggleable = FeatureRegistrySeed.IsToggleable(entry.Status) && entry.Visible;
        return new FeatureRegistryDto(
            FeatureKey: entry.FeatureKey,
            ModuleKey: entry.ModuleKey,
            ModuleCode: entry.ModuleKey,
            Name: entry.Name,
            DisplayName: entry.DisplayName,
            Description: entry.Description,
            Category: entry.Category,
            Icon: entry.Icon,
            Route: entry.Route,
            SortOrder: entry.SortOrder,
            Visible: entry.Visible,
            Status: entry.Status,
            IsMobileSupported: entry.IsMobileSupported,
            IsAISupported: entry.IsAISupported,
            IsGPSSupported: entry.IsGPSSupported,
            DocumentationUrl: entry.DocumentationUrl,
            IsActive: !string.Equals(entry.Status, "Deprecated", StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(entry.Status, "Disabled", StringComparison.OrdinalIgnoreCase),
            IsEnabled: isEnabled,
            IsModuleInstalled: moduleInstalled,
            CanToggle: toggleable && moduleInstalled);
    }

    public static CompanyFeatureDto ToCompanyFeatureDto(FeatureRegistryDto dto)
        => new(
            dto.FeatureKey,
            dto.ModuleKey,
            dto.Name,
            dto.Description,
            dto.IsEnabled,
            dto.SortOrder,
            dto.DisplayName,
            dto.Category,
            dto.Icon,
            dto.Route,
            dto.Status,
            dto.IsMobileSupported,
            dto.IsAISupported,
            dto.IsGPSSupported,
            dto.Visible,
            dto.CanToggle);
}

public record FeatureRegistryDto(
    string FeatureKey,
    string ModuleKey,
    string ModuleCode,
    string Name,
    string DisplayName,
    string? Description,
    string? Category,
    string? Icon,
    string? Route,
    int SortOrder,
    bool Visible,
    string Status,
    bool IsMobileSupported,
    bool IsAISupported,
    bool IsGPSSupported,
    string? DocumentationUrl,
    bool IsActive,
    bool IsEnabled = false,
    bool IsModuleInstalled = false,
    bool CanToggle = false);

public record GetFeatureRegistryCatalogQuery : IRequest<ApiResponse<IReadOnlyList<FeatureRegistryDto>>>;

public record GetFeatureByKeyQuery(string Key) : IRequest<ApiResponse<FeatureRegistryDto>>;

public record GetCompanyFeatureRegistryQuery(int? TenantId = null)
    : IRequest<ApiResponse<IReadOnlyList<FeatureRegistryDto>>>;

public record SetCompanyFeaturesCommand(int TenantId, IReadOnlyList<string> EnabledFeatureKeys)
    : IRequest<ApiResponse<bool>>;

public class GetFeatureRegistryCatalogQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetFeatureRegistryCatalogQuery, ApiResponse<IReadOnlyList<FeatureRegistryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<FeatureRegistryDto>>> Handle(
        GetFeatureRegistryCatalogQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await platformRepository.LoadVisibleFeaturesAsync(cancellationToken: cancellationToken);
        var dtos = rows.Select(r => FeatureRegistryQueries.ToRegistryDto(r)).ToList();
        return ApiResponse<IReadOnlyList<FeatureRegistryDto>>.SuccessResponse(dtos);
    }
}

public class GetFeatureByKeyQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetFeatureByKeyQuery, ApiResponse<FeatureRegistryDto>>
{
    public async Task<ApiResponse<FeatureRegistryDto>> Handle(
        GetFeatureByKeyQuery request,
        CancellationToken cancellationToken)
    {
        var row = await platformRepository.LoadFeatureByKeyAsync(request.Key, cancellationToken);
        if (row is null)
        {
            var seed = FeatureRegistrySeed.Find(request.Key);
            if (seed is null)
                return ApiResponse<FeatureRegistryDto>.FailResponse("Feature not found.");
            return ApiResponse<FeatureRegistryDto>.SuccessResponse(
                FeatureRegistryQueries.FromSeed(seed));
        }

        return ApiResponse<FeatureRegistryDto>.SuccessResponse(
            FeatureRegistryQueries.ToRegistryDto(row));
    }
}

public class GetCompanyFeatureRegistryQueryHandler(
    IPlatformRepository platformRepository,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetCompanyFeatureRegistryQuery, ApiResponse<IReadOnlyList<FeatureRegistryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<FeatureRegistryDto>>> Handle(
        GetCompanyFeatureRegistryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? tenantContext.GetRequiredTenantId();
        platformScope.EnsureTenantAccess(tenantId);

        var rows = await platformRepository.LoadCompanyFeaturesAsync(tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<FeatureRegistryDto>>.SuccessResponse(rows);
    }
}

public class SetCompanyFeaturesCommandHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope,
    ICurrentUserService currentUser)
    : IRequestHandler<SetCompanyFeaturesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        SetCompanyFeaturesCommand request,
        CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        var enabledKeys = request.EnabledFeatureKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var installed = (await platformRepository.GetInstalledModuleCodesAsync(request.TenantId, cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalog = await platformRepository.LoadVisibleFeaturesAsync(cancellationToken: cancellationToken);

        var toggleableKeys = catalog
            .Where(f => FeatureRegistrySeed.IsToggleable(f.Status)
                        && f.Visible
                        && installed.Contains(f.ModuleKey))
            .Select(f => f.FeatureKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalid = enabledKeys.Where(k => !toggleableKeys.Contains(k)).ToList();
        if (invalid.Count > 0)
            return ApiResponse<bool>.FailResponse(
                $"Cannot enable features (not Active/Beta under an installed module): {string.Join(", ", invalid)}");

        await platformRepository.SetCompanyFeaturesAsync(
            request.TenantId, enabledKeys, toggleableKeys, currentUser.UserId, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true);
    }
}
