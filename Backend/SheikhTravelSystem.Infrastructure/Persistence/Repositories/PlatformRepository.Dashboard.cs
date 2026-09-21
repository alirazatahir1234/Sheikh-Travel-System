using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<bool> DashboardTablesExistAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DashboardDefinitions')
            THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken));
        return n == 1;
    }

    public async Task<IReadOnlyList<DashboardBuilderQueries.DashboardRow>> LoadDashboardDefinitionsAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<DashboardBuilderQueries.DashboardRow>(new CommandDefinition("""
            SELECT DashboardKey, DisplayName, Description, Audience, DefaultWorkspaceKey, Category,
                   SortOrder, Status, Visible, IsSystem, IsActive
            FROM DashboardDefinitions
            WHERE (@ActiveOnly = 0 OR (IsActive = 1 AND Visible = 1 AND Status = N'Active'))
            ORDER BY SortOrder, DisplayName
            """,
            new { ActiveOnly = activeOnly ? 1 : 0 },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<DashboardBuilderQueries.WidgetRow>> LoadDashboardWidgetsAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<DashboardBuilderQueries.WidgetRow>(new CommandDefinition("""
            SELECT WidgetKey, DisplayName, Category, Icon, PermissionCode, FeatureKey, ModuleKey,
                   SupportsErp, SupportsMobile, SortOrder, Status, Visible, IsActive
            FROM DashboardWidgetDefinitions
            WHERE (@ActiveOnly = 0 OR (IsActive = 1 AND Visible = 1 AND Status = N'Active'))
            ORDER BY SortOrder, DisplayName
            """,
            new { ActiveOnly = activeOnly ? 1 : 0 },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<DashboardBuilderQueries.LayoutJoinRow>> LoadDashboardLayoutAsync(
        string dashboardKey, bool visibleOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<DashboardBuilderQueries.LayoutJoinRow>(new CommandDefinition("""
            SELECT l.DashboardKey, l.WidgetKey, l.SortOrder, l.IsVisible,
                   w.DisplayName, w.Category, w.Icon, w.PermissionCode, w.FeatureKey, w.ModuleKey,
                   w.SupportsErp, w.SupportsMobile
            FROM DashboardLayouts l
            INNER JOIN DashboardWidgetDefinitions w ON w.WidgetKey = l.WidgetKey
            WHERE l.DashboardKey = @DashboardKey
              AND w.IsActive = 1 AND w.Visible = 1
              AND (@VisibleOnly = 0 OR l.IsVisible = 1)
            ORDER BY l.SortOrder, w.DisplayName
            """,
            new { DashboardKey = dashboardKey, VisibleOnly = visibleOnly ? 1 : 0 },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<Dictionary<string, int>> LoadDashboardWidgetCountsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<(string DashboardKey, int Cnt)>(new CommandDefinition("""
            SELECT DashboardKey, COUNT(*) AS Cnt
            FROM DashboardLayouts
            WHERE IsVisible = 1
            GROUP BY DashboardKey
            """, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.DashboardKey, r => r.Cnt, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ResolvedDashboardDto?> ResolveDashboardForUserAsync(
        string? userDashboardKey,
        string? workspaceDashboardKey,
        string? workspaceKey,
        string? roleCode,
        bool preferMobile,
        HashSet<string> permissions,
        HashSet<string> enabledModules,
        Dictionary<string, bool> featureFlags,
        CancellationToken cancellationToken = default)
    {
        if (!await DashboardTablesExistAsync(cancellationToken))
            return null;

        var catalog = await LoadDashboardDefinitionsAsync(activeOnly: true, cancellationToken: cancellationToken);
        if (catalog.Count == 0) return null;

        var key = DashboardBuilderQueries.ResolveKey(catalog, userDashboardKey, workspaceDashboardKey, workspaceKey, roleCode, preferMobile);
        var def = catalog.FirstOrDefault(d => d.DashboardKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                  ?? catalog[0];

        string source;
        if (!string.IsNullOrWhiteSpace(userDashboardKey) &&
            userDashboardKey.Equals(def.DashboardKey, StringComparison.OrdinalIgnoreCase))
            source = "user";
        else if (!string.IsNullOrWhiteSpace(workspaceDashboardKey) &&
                 workspaceDashboardKey.Equals(def.DashboardKey, StringComparison.OrdinalIgnoreCase))
            source = "workspace";
        else
            source = "default";

        var layout = await LoadDashboardLayoutAsync(def.DashboardKey, visibleOnly: true, cancellationToken: cancellationToken);
        var filtered = layout
            .Where(w => DashboardBuilderQueries.WidgetAllowed(w, permissions, enabledModules, featureFlags, preferMobile))
            .Select(DashboardBuilderQueries.ToLayoutItem)
            .ToList();

        return new ResolvedDashboardDto(
            def.DashboardKey,
            def.DisplayName,
            def.Audience,
            source,
            filtered.Select(w => w.WidgetKey).ToList(),
            filtered);
    }

    public async Task<int> UpdateDashboardDefinitionAsync(
        string key, UpdateDashboardDefinitionPayload p, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE DashboardDefinitions SET
                DisplayName = @DisplayName,
                Description = @Description,
                Audience = @Audience,
                DefaultWorkspaceKey = @DefaultWorkspaceKey,
                Category = @Category,
                SortOrder = @SortOrder,
                Visible = @Visible,
                IsActive = @IsActive,
                UpdatedAt = SYSUTCDATETIME()
            WHERE DashboardKey = @Key
            """,
            new
            {
                Key = key,
                DisplayName = p.DisplayName.Trim(),
                p.Description,
                Audience = string.IsNullOrWhiteSpace(p.Audience) ? "Both" : p.Audience.Trim(),
                p.DefaultWorkspaceKey,
                p.Category,
                p.SortOrder,
                Visible = p.Visible,
                IsActive = p.IsActive
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DashboardExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM DashboardDefinitions WHERE DashboardKey = @Key) THEN 1 ELSE 0 END",
            new { Key = key },
            cancellationToken: cancellationToken));
        return exists == 1;
    }

    public async Task UpdateDashboardLayoutAsync(
        string key, IReadOnlyList<UpdateDashboardLayoutItemPayload> items, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM DashboardLayouts WHERE DashboardKey = @Key",
                new { Key = key },
                transaction: tx,
                cancellationToken: cancellationToken));

            foreach (var item in items)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO DashboardLayouts (DashboardKey, WidgetKey, SortOrder, IsVisible)
                    VALUES (@DashboardKey, @WidgetKey, @SortOrder, @IsVisible)
                    """,
                    new
                    {
                        DashboardKey = key,
                        item.WidgetKey,
                        item.SortOrder,
                        IsVisible = item.IsVisible
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
