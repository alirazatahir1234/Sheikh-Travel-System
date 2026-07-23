using System.Data;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Company;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Shared SQL/mapping for Stage 5 Feature Registry reads.</summary>
internal static class FeatureRegistryQueries
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

    public static async Task<IReadOnlyList<FeatureRow>> LoadVisibleAsync(
        IDbConnection connection,
        CancellationToken cancellationToken,
        bool activeOnly = false)
    {
        try
        {
            var sql = SelectSql + """
                WHERE COALESCE(fd.Visible, 1) = 1
                """ + (activeOnly ? " AND COALESCE(fd.IsActive, 1) = 1" : "") + """
                ORDER BY fd.SortOrder, fd.FeatureKey
                """;
            return (await connection.QueryAsync<FeatureRow>(
                new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            return FeatureRegistrySeed.All
                .Where(e => e.Visible && (!activeOnly || FeatureRegistrySeed.IsToggleable(e.Status)))
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.FeatureKey)
                .Select(e => new FeatureRow
                {
                    FeatureKey = e.FeatureKey,
                    ModuleKey = e.ModuleKey,
                    Name = e.Name,
                    DisplayName = e.DisplayName,
                    Description = e.Description,
                    Category = e.Category,
                    Icon = e.Icon,
                    Route = e.Route,
                    SortOrder = e.SortOrder,
                    Visible = e.Visible,
                    Status = e.Status,
                    IsMobileSupported = e.IsMobileSupported,
                    IsAISupported = e.IsAISupported,
                    IsGPSSupported = e.IsGPSSupported,
                    DocumentationUrl = e.DocumentationUrl,
                    IsActive = FeatureRegistrySeed.IsToggleable(e.Status)
                })
                .ToList();
        }
    }

    public static async Task<HashSet<string>> LoadInstalledModuleCodesAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var codes = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<Dictionary<string, bool>> LoadTenantFeatureFlagsAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await connection.QueryAsync<(string FeatureKey, bool IsEnabled)>(
                new CommandDefinition("""
                    SELECT FeatureKey, IsEnabled FROM TenantFeatures WHERE TenantId = @TenantId
                    """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
            return rows.ToDictionary(r => r.FeatureKey, r => r.IsEnabled, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static async Task<IReadOnlyList<FeatureRegistryDto>> LoadCompanyFeaturesAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadVisibleAsync(connection, cancellationToken);
        var installed = await LoadInstalledModuleCodesAsync(connection, tenantId, cancellationToken);
        var flags = await LoadTenantFeatureFlagsAsync(connection, tenantId, cancellationToken);

        return rows.Select(row =>
        {
            var moduleInstalled = installed.Contains(row.ModuleKey);
            var isEnabled = flags.TryGetValue(row.FeatureKey, out var en)
                ? en
                : moduleInstalled; // default on when module installed and no row yet
            return ToRegistryDto(row, isEnabled, moduleInstalled);
        }).ToList();
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

public class GetFeatureRegistryCatalogQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetFeatureRegistryCatalogQuery, ApiResponse<IReadOnlyList<FeatureRegistryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<FeatureRegistryDto>>> Handle(
        GetFeatureRegistryCatalogQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await FeatureRegistryQueries.LoadVisibleAsync(connection, cancellationToken);
        var dtos = rows.Select(r => FeatureRegistryQueries.ToRegistryDto(r)).ToList();
        return ApiResponse<IReadOnlyList<FeatureRegistryDto>>.SuccessResponse(dtos);
    }
}

public class GetFeatureByKeyQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetFeatureByKeyQuery, ApiResponse<FeatureRegistryDto>>
{
    public async Task<ApiResponse<FeatureRegistryDto>> Handle(
        GetFeatureByKeyQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<FeatureRegistryQueries.FeatureRow>(
                new CommandDefinition(
                    FeatureRegistryQueries.SelectSql + " WHERE fd.FeatureKey = @Key",
                    new { Key = request.Key },
                    cancellationToken: cancellationToken));
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
        catch
        {
            var seed = FeatureRegistrySeed.Find(request.Key);
            if (seed is null)
                return ApiResponse<FeatureRegistryDto>.FailResponse("Feature not found.");
            return ApiResponse<FeatureRegistryDto>.SuccessResponse(
                FeatureRegistryQueries.FromSeed(seed));
        }
    }
}

public class GetCompanyFeatureRegistryQueryHandler(
    IDbConnectionFactory dbFactory,
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

        using var connection = dbFactory.CreateConnection();
        var rows = await FeatureRegistryQueries.LoadCompanyFeaturesAsync(
            connection, tenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<FeatureRegistryDto>>.SuccessResponse(rows);
    }
}

public class SetCompanyFeaturesCommandHandler(
    IDbConnectionFactory dbFactory,
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

        using var connection = dbFactory.CreateConnection();
        var installed = await FeatureRegistryQueries.LoadInstalledModuleCodesAsync(
            connection, request.TenantId, cancellationToken);
        var catalog = await FeatureRegistryQueries.LoadVisibleAsync(connection, cancellationToken);

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

        var userId = currentUser.UserId;
        var now = DateTime.UtcNow;

        // Upsert all toggleable features for this company; disable those not in the enabled set.
        foreach (var key in toggleableKeys)
        {
            var enabled = enabledKeys.Contains(key);
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM TenantFeatures WHERE TenantId = @TenantId AND FeatureKey = @FeatureKey)
                    UPDATE TenantFeatures
                    SET IsEnabled = @IsEnabled,
                        EnabledBy = CASE WHEN @IsEnabled = 1 THEN @EnabledBy ELSE EnabledBy END,
                        EnabledDate = CASE WHEN @IsEnabled = 1 THEN COALESCE(EnabledDate, @Now) ELSE EnabledDate END,
                        LastModified = @Now
                    WHERE TenantId = @TenantId AND FeatureKey = @FeatureKey;
                ELSE
                    INSERT INTO TenantFeatures (TenantId, FeatureKey, IsEnabled, EnabledBy, EnabledDate, LastModified)
                    VALUES (@TenantId, @FeatureKey, @IsEnabled, @EnabledBy, CASE WHEN @IsEnabled = 1 THEN @Now ELSE NULL END, @Now);
                """,
                new
                {
                    TenantId = request.TenantId,
                    FeatureKey = key,
                    IsEnabled = enabled,
                    EnabledBy = userId,
                    Now = now
                },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true);
    }
}
