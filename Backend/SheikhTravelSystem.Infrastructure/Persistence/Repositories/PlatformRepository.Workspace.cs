using Dapper;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<IReadOnlyList<WorkspaceBuilderQueries.WorkspaceRow>> LoadWorkspaceCatalogAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var rows = await connection.QueryAsync<WorkspaceBuilderQueries.WorkspaceRow>(new CommandDefinition("""
                SELECT WorkspaceKey, DisplayName, Description, Category, Icon, HomeRoute, SortOrder,
                       Visible, IsActive, IsMobileSupported, ModuleKeysJson, FeatureKey, DefaultDashboardKey
                FROM WorkspaceDefinitions
                WHERE (@ActiveOnly = 0 OR (IsActive = 1 AND Visible = 1))
                ORDER BY SortOrder, DisplayName
                """,
                new { ActiveOnly = activeOnly ? 1 : 0 },
                cancellationToken: cancellationToken));
            return rows.ToList();
        }
        catch
        {
            return Array.Empty<WorkspaceBuilderQueries.WorkspaceRow>();
        }
    }

    public async Task<Dictionary<string, bool>> LoadTenantWorkspaceFlagsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var rows = await connection.QueryAsync<(string WorkspaceKey, bool IsEnabled)>(
                new CommandDefinition("""
                    SELECT WorkspaceKey, IsEnabled FROM TenantWorkspaces WHERE TenantId = @TenantId
                    """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
            return rows.ToDictionary(r => r.WorkspaceKey, r => r.IsEnabled, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task SetCompanyWorkspacesAsync(
        int tenantId, IReadOnlySet<string> toggleableKeys, IReadOnlyList<string> enabledKeys, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var now = DateTime.UtcNow;
        var enabledSet = enabledKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in toggleableKeys)
        {
            var enabled = enabledSet.Contains(key);
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM TenantWorkspaces WHERE TenantId = @TenantId AND WorkspaceKey = @WorkspaceKey)
                    UPDATE TenantWorkspaces
                    SET IsEnabled = @IsEnabled,
                        EnabledBy = CASE WHEN @IsEnabled = 1 THEN @EnabledBy ELSE EnabledBy END,
                        EnabledDate = CASE WHEN @IsEnabled = 1 THEN COALESCE(EnabledDate, @Now) ELSE EnabledDate END,
                        LastModified = @Now
                    WHERE TenantId = @TenantId AND WorkspaceKey = @WorkspaceKey;
                ELSE
                    INSERT INTO TenantWorkspaces (TenantId, WorkspaceKey, IsEnabled, EnabledBy, EnabledDate, LastModified)
                    VALUES (@TenantId, @WorkspaceKey, @IsEnabled, @EnabledBy, @Now, @Now);
                """,
                new
                {
                    TenantId = tenantId,
                    WorkspaceKey = key,
                    IsEnabled = enabled,
                    EnabledBy = userId,
                    Now = now
                },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<int> UpdateWorkspaceDefinitionAsync(
        string key, UpdateWorkspaceDefinitionPayload p, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WorkspaceDefinitions SET
                DisplayName = @DisplayName,
                Description = @Description,
                Category = @Category,
                Icon = @Icon,
                HomeRoute = @HomeRoute,
                SortOrder = @SortOrder,
                Visible = @Visible,
                IsActive = @IsActive,
                IsMobileSupported = @IsMobileSupported,
                ModuleKeysJson = @ModuleKeysJson,
                FeatureKey = @FeatureKey,
                DefaultDashboardKey = @DefaultDashboardKey,
                UpdatedAt = SYSUTCDATETIME()
            WHERE WorkspaceKey = @WorkspaceKey;
            """,
            new
            {
                WorkspaceKey = key,
                p.DisplayName,
                p.Description,
                p.Category,
                p.Icon,
                HomeRoute = string.IsNullOrWhiteSpace(p.HomeRoute) ? "/dashboard" : p.HomeRoute.Trim(),
                p.SortOrder,
                p.Visible,
                p.IsActive,
                p.IsMobileSupported,
                ModuleKeysJson = WorkspaceBuilderQueries.SerializeModuleKeys(p.ModuleKeys),
                p.FeatureKey,
                p.DefaultDashboardKey
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> WorkspaceKeyExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM WorkspaceDefinitions WHERE WorkspaceKey = @Key) THEN 1 ELSE 0 END",
            new { Key = key }, cancellationToken: cancellationToken));
        return exists == 1;
    }

    public async Task CreateWorkspaceDefinitionAsync(
        string key, CreateWorkspaceDefinitionPayload p, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO WorkspaceDefinitions (
                WorkspaceKey, DisplayName, Description, Category, Icon, HomeRoute, SortOrder,
                Visible, IsActive, IsMobileSupported, ModuleKeysJson, FeatureKey, DefaultDashboardKey)
            VALUES (
                @WorkspaceKey, @DisplayName, @Description, @Category, @Icon, @HomeRoute, @SortOrder,
                @Visible, 1, @IsMobileSupported, @ModuleKeysJson, @FeatureKey, @DefaultDashboardKey);

            INSERT INTO TenantWorkspaces (TenantId, WorkspaceKey, IsEnabled, EnabledDate, LastModified)
            SELECT t.Id, @WorkspaceKey, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM Tenants t
            WHERE NOT EXISTS (
                SELECT 1 FROM TenantWorkspaces tw
                WHERE tw.TenantId = t.Id AND tw.WorkspaceKey = @WorkspaceKey);
            """,
            new
            {
                WorkspaceKey = key,
                p.DisplayName,
                p.Description,
                p.Category,
                p.Icon,
                HomeRoute = string.IsNullOrWhiteSpace(p.HomeRoute) ? "/dashboard" : p.HomeRoute.Trim(),
                p.SortOrder,
                Visible = p.Visible,
                IsMobileSupported = p.IsMobileSupported,
                ModuleKeysJson = WorkspaceBuilderQueries.SerializeModuleKeys(p.ModuleKeys),
                p.FeatureKey,
                p.DefaultDashboardKey
            },
            cancellationToken: cancellationToken));
    }

    public async Task<int> DeactivateWorkspaceDefinitionAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WorkspaceDefinitions SET
                IsActive = 0, Visible = 0, UpdatedAt = SYSUTCDATETIME()
            WHERE WorkspaceKey = @Key;
            """, new { Key = key }, cancellationToken: cancellationToken));
    }
}
