using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<IReadOnlyList<FeatureRegistryQueries.FeatureRow>> LoadVisibleFeaturesAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var sql = FeatureRegistryQueries.SelectSql + """
                WHERE COALESCE(fd.Visible, 1) = 1
                """ + (activeOnly ? " AND COALESCE(fd.IsActive, 1) = 1" : "") + """
                ORDER BY fd.SortOrder, fd.FeatureKey
                """;
            return (await connection.QueryAsync<FeatureRegistryQueries.FeatureRow>(
                new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
        }
        catch
        {
            return FeatureRegistrySeed.All
                .Where(e => e.Visible && (!activeOnly || FeatureRegistrySeed.IsToggleable(e.Status)))
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.FeatureKey)
                .Select(e => new FeatureRegistryQueries.FeatureRow
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

    public async Task<FeatureRegistryQueries.FeatureRow?> LoadFeatureByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<FeatureRegistryQueries.FeatureRow>(
                new CommandDefinition(
                    FeatureRegistryQueries.SelectSql + " WHERE fd.FeatureKey = @Key",
                    new { Key = key },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<FeatureRegistryDto>> LoadCompanyFeaturesAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await LoadVisibleFeaturesAsync(cancellationToken: cancellationToken);
        var installed = (await GetInstalledModuleCodesAsync(tenantId, cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var flags = await LoadTenantFeatureFlagsAsync(tenantId, cancellationToken);

        return rows.Select(row =>
        {
            var moduleInstalled = installed.Contains(row.ModuleKey);
            var isEnabled = flags.TryGetValue(row.FeatureKey, out var en)
                ? en
                : moduleInstalled;
            return FeatureRegistryQueries.ToRegistryDto(row, isEnabled, moduleInstalled);
        }).ToList();
    }

    public async Task<Dictionary<string, bool>> LoadTenantFeatureFlagsAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
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

    public async Task SetCompanyFeaturesAsync(
        int tenantId,
        IReadOnlyList<string> enabledKeys,
        IReadOnlySet<string> toggleableKeys,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var enabled = enabledKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var key in toggleableKeys)
        {
            var isEnabled = enabled.Contains(key);
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
                    TenantId = tenantId,
                    FeatureKey = key,
                    IsEnabled = isEnabled,
                    EnabledBy = userId,
                    Now = now
                },
                cancellationToken: cancellationToken));
        }
    }
}
