using System.Data;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Platform;

public record DashboardDefinitionDto(
    string DashboardKey,
    string DisplayName,
    string? Description,
    string Audience,
    string? DefaultWorkspaceKey,
    string? Category,
    int SortOrder,
    string Status,
    bool Visible,
    bool IsSystem,
    bool IsActive,
    int WidgetCount = 0);

public record DashboardWidgetDefinitionDto(
    string WidgetKey,
    string DisplayName,
    string? Category,
    string? Icon,
    string? PermissionCode,
    string? FeatureKey,
    string? ModuleKey,
    bool SupportsErp,
    bool SupportsMobile,
    int SortOrder,
    string Status,
    bool Visible,
    bool IsActive);

public record DashboardLayoutItemDto(
    string WidgetKey,
    string DisplayName,
    string? Category,
    string? Icon,
    int SortOrder,
    bool IsVisible,
    string? PermissionCode = null,
    string? FeatureKey = null,
    string? ModuleKey = null,
    bool SupportsErp = true,
    bool SupportsMobile = true);

public record DashboardDetailDto(
    DashboardDefinitionDto Definition,
    IReadOnlyList<DashboardLayoutItemDto> Layout);

public record ResolvedDashboardDto(
    string Key,
    string DisplayName,
    string Audience,
    string Source,
    IReadOnlyList<string> WidgetKeys,
    IReadOnlyList<DashboardLayoutItemDto> Widgets);

public record CompanyDashboardSummaryDto(
    string? Key,
    string? DisplayName,
    IReadOnlyList<string> WidgetKeys,
    string Source);

public record UpdateDashboardDefinitionPayload(
    string DisplayName,
    string? Description,
    string Audience,
    string? DefaultWorkspaceKey,
    string? Category,
    int SortOrder,
    bool Visible,
    bool IsActive);

public record UpdateDashboardLayoutPayload(
    IReadOnlyList<UpdateDashboardLayoutItemPayload> Items);

public record UpdateDashboardLayoutItemPayload(
    string WidgetKey,
    int SortOrder,
    bool IsVisible = true);

public record GetDashboardCatalogQuery(bool ActiveOnly = false)
    : IRequest<ApiResponse<IReadOnlyList<DashboardDefinitionDto>>>;
public record GetDashboardWidgetsQuery(bool ActiveOnly = false)
    : IRequest<ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>>;
public record GetDashboardByKeyQuery(string Key)
    : IRequest<ApiResponse<DashboardDetailDto>>;
public record GetMyDashboardQuery(string? Audience = null)
    : IRequest<ApiResponse<ResolvedDashboardDto>>;
public record UpdateDashboardDefinitionCommand(string Key, UpdateDashboardDefinitionPayload Payload)
    : IRequest<ApiResponse<bool>>;
public record UpdateDashboardLayoutCommand(string Key, UpdateDashboardLayoutPayload Payload)
    : IRequest<ApiResponse<bool>>;

public static class DashboardBuilderQueries
{
    public sealed record DashboardRow(
        string DashboardKey,
        string DisplayName,
        string? Description,
        string Audience,
        string? DefaultWorkspaceKey,
        string? Category,
        int SortOrder,
        string Status,
        bool Visible,
        bool IsSystem,
        bool IsActive);

    public sealed record WidgetRow(
        string WidgetKey,
        string DisplayName,
        string? Category,
        string? Icon,
        string? PermissionCode,
        string? FeatureKey,
        string? ModuleKey,
        bool SupportsErp,
        bool SupportsMobile,
        int SortOrder,
        string Status,
        bool Visible,
        bool IsActive);

    public sealed record LayoutJoinRow(
        string DashboardKey,
        string WidgetKey,
        int SortOrder,
        bool IsVisible,
        string DisplayName,
        string? Category,
        string? Icon,
        string? PermissionCode,
        string? FeatureKey,
        string? ModuleKey,
        bool SupportsErp,
        bool SupportsMobile);

    public static async Task<bool> TablesExistAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DashboardDefinitions')
            THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken));
        return n == 1;
    }

    public static async Task<IReadOnlyList<DashboardRow>> LoadDefinitionsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken,
        bool activeOnly = false)
    {
        return (await connection.QueryAsync<DashboardRow>(new CommandDefinition("""
            SELECT DashboardKey, DisplayName, Description, Audience, DefaultWorkspaceKey, Category,
                   SortOrder, Status, Visible, IsSystem, IsActive
            FROM DashboardDefinitions
            WHERE (@ActiveOnly = 0 OR (IsActive = 1 AND Visible = 1 AND Status = N'Active'))
            ORDER BY SortOrder, DisplayName
            """,
            new { ActiveOnly = activeOnly ? 1 : 0 },
            cancellationToken: cancellationToken))).ToList();
    }

    public static async Task<IReadOnlyList<WidgetRow>> LoadWidgetsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken,
        bool activeOnly = false)
    {
        return (await connection.QueryAsync<WidgetRow>(new CommandDefinition("""
            SELECT WidgetKey, DisplayName, Category, Icon, PermissionCode, FeatureKey, ModuleKey,
                   SupportsErp, SupportsMobile, SortOrder, Status, Visible, IsActive
            FROM DashboardWidgetDefinitions
            WHERE (@ActiveOnly = 0 OR (IsActive = 1 AND Visible = 1 AND Status = N'Active'))
            ORDER BY SortOrder, DisplayName
            """,
            new { ActiveOnly = activeOnly ? 1 : 0 },
            cancellationToken: cancellationToken))).ToList();
    }

    public static async Task<IReadOnlyList<LayoutJoinRow>> LoadLayoutAsync(
        IDbConnection connection,
        string dashboardKey,
        CancellationToken cancellationToken,
        bool visibleOnly = false)
    {
        return (await connection.QueryAsync<LayoutJoinRow>(new CommandDefinition("""
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

    public static async Task<Dictionary<string, int>> LoadWidgetCountsAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(string DashboardKey, int Cnt)>(new CommandDefinition("""
            SELECT DashboardKey, COUNT(*) AS Cnt
            FROM DashboardLayouts
            WHERE IsVisible = 1
            GROUP BY DashboardKey
            """, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.DashboardKey, r => r.Cnt, StringComparer.OrdinalIgnoreCase);
    }

    public static DashboardDefinitionDto ToDefinitionDto(DashboardRow row, int widgetCount = 0) => new(
        row.DashboardKey, row.DisplayName, row.Description, row.Audience, row.DefaultWorkspaceKey,
        row.Category, row.SortOrder, row.Status, row.Visible, row.IsSystem, row.IsActive, widgetCount);

    public static DashboardWidgetDefinitionDto ToWidgetDto(WidgetRow row) => new(
        row.WidgetKey, row.DisplayName, row.Category, row.Icon, row.PermissionCode, row.FeatureKey,
        row.ModuleKey, row.SupportsErp, row.SupportsMobile, row.SortOrder, row.Status, row.Visible, row.IsActive);

    public static DashboardLayoutItemDto ToLayoutItem(LayoutJoinRow row) => new(
        row.WidgetKey, row.DisplayName, row.Category, row.Icon, row.SortOrder, row.IsVisible,
        row.PermissionCode, row.FeatureKey, row.ModuleKey, row.SupportsErp, row.SupportsMobile);

    public static bool WidgetAllowed(
        LayoutJoinRow widget,
        HashSet<string> permissions,
        HashSet<string> enabledModules,
        Dictionary<string, bool> featureFlags,
        bool preferMobile)
    {
        if (preferMobile && !widget.SupportsMobile) return false;
        if (!preferMobile && !widget.SupportsErp) return false;

        if (!string.IsNullOrWhiteSpace(widget.PermissionCode) &&
            !permissions.Contains(widget.PermissionCode))
            return false;

        if (!string.IsNullOrWhiteSpace(widget.ModuleKey) &&
            enabledModules.Count > 0 &&
            !enabledModules.Contains(widget.ModuleKey))
            return false;

        if (!string.IsNullOrWhiteSpace(widget.FeatureKey) && featureFlags.Count > 0)
        {
            if (featureFlags.TryGetValue(widget.FeatureKey, out var enabled) && !enabled)
                return false;
        }

        return true;
    }

    public static string ResolveKey(
        IReadOnlyList<DashboardRow> catalog,
        string? userDashboardKey,
        string? workspaceDashboardKey,
        string? workspaceKey,
        string? roleCode,
        bool preferMobile)
    {
        bool Exists(string? key) =>
            !string.IsNullOrWhiteSpace(key) &&
            catalog.Any(d => d.DashboardKey.Equals(key, StringComparison.OrdinalIgnoreCase)
                             && d.IsActive && d.Visible);

        if (Exists(userDashboardKey)) return userDashboardKey!;
        if (Exists(workspaceDashboardKey)) return workspaceDashboardKey!;

        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            var byWs = catalog.FirstOrDefault(d =>
                d.IsActive && d.Visible &&
                string.Equals(d.DefaultWorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase));
            if (byWs is not null) return byWs.DashboardKey;
        }

        var roleDefault = DashboardRegistrySeed.RoleDefaultDashboard(roleCode, preferMobile);
        if (Exists(roleDefault)) return roleDefault;

        var audience = preferMobile ? "Mobile" : "ERP";
        var byAudience = catalog.FirstOrDefault(d =>
            d.IsActive && d.Visible &&
            d.Audience.Equals(audience, StringComparison.OrdinalIgnoreCase));
        if (byAudience is not null) return byAudience.DashboardKey;

        return DashboardRegistrySeed.AudienceFallback(preferMobile);
    }

    public static async Task<ResolvedDashboardDto?> ResolveForUserAsync(
        IDbConnection connection,
        string? userDashboardKey,
        string? workspaceDashboardKey,
        string? workspaceKey,
        string? roleCode,
        bool preferMobile,
        HashSet<string> permissions,
        HashSet<string> enabledModules,
        Dictionary<string, bool> featureFlags,
        CancellationToken cancellationToken)
    {
        if (!await TablesExistAsync(connection, cancellationToken))
            return null;

        var catalog = await LoadDefinitionsAsync(connection, cancellationToken, activeOnly: true);
        if (catalog.Count == 0) return null;

        var key = ResolveKey(catalog, userDashboardKey, workspaceDashboardKey, workspaceKey, roleCode, preferMobile);
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

        var layout = await LoadLayoutAsync(connection, def.DashboardKey, cancellationToken, visibleOnly: true);
        var filtered = layout
            .Where(w => WidgetAllowed(w, permissions, enabledModules, featureFlags, preferMobile))
            .Select(ToLayoutItem)
            .ToList();

        return new ResolvedDashboardDto(
            def.DashboardKey,
            def.DisplayName,
            def.Audience,
            source,
            filtered.Select(w => w.WidgetKey).ToList(),
            filtered);
    }
}

public class GetDashboardCatalogQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDashboardCatalogQuery, ApiResponse<IReadOnlyList<DashboardDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DashboardDefinitionDto>>> Handle(
        GetDashboardCatalogQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await DashboardBuilderQueries.TablesExistAsync(connection, cancellationToken))
            return ApiResponse<IReadOnlyList<DashboardDefinitionDto>>.SuccessResponse([]);

        var rows = await DashboardBuilderQueries.LoadDefinitionsAsync(connection, cancellationToken, request.ActiveOnly);
        var counts = await DashboardBuilderQueries.LoadWidgetCountsAsync(connection, cancellationToken);
        var dtos = rows.Select(r => DashboardBuilderQueries.ToDefinitionDto(
            r, counts.GetValueOrDefault(r.DashboardKey))).ToList();
        return ApiResponse<IReadOnlyList<DashboardDefinitionDto>>.SuccessResponse(dtos);
    }
}

public class GetDashboardWidgetsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDashboardWidgetsQuery, ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>> Handle(
        GetDashboardWidgetsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await DashboardBuilderQueries.TablesExistAsync(connection, cancellationToken))
            return ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>.SuccessResponse([]);

        var rows = await DashboardBuilderQueries.LoadWidgetsAsync(connection, cancellationToken, request.ActiveOnly);
        return ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>.SuccessResponse(
            rows.Select(DashboardBuilderQueries.ToWidgetDto).ToList());
    }
}

public class GetDashboardByKeyQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDashboardByKeyQuery, ApiResponse<DashboardDetailDto>>
{
    public async Task<ApiResponse<DashboardDetailDto>> Handle(
        GetDashboardByKeyQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await DashboardBuilderQueries.TablesExistAsync(connection, cancellationToken))
            return ApiResponse<DashboardDetailDto>.FailResponse("Dashboard catalog is not available.");

        var rows = await DashboardBuilderQueries.LoadDefinitionsAsync(connection, cancellationToken);
        var def = rows.FirstOrDefault(r =>
            r.DashboardKey.Equals(request.Key, StringComparison.OrdinalIgnoreCase));
        if (def is null)
            return ApiResponse<DashboardDetailDto>.FailResponse("Dashboard not found.");

        var layout = await DashboardBuilderQueries.LoadLayoutAsync(connection, def.DashboardKey, cancellationToken);
        var dto = new DashboardDetailDto(
            DashboardBuilderQueries.ToDefinitionDto(def, layout.Count(l => l.IsVisible)),
            layout.Select(DashboardBuilderQueries.ToLayoutItem).ToList());
        return ApiResponse<DashboardDetailDto>.SuccessResponse(dto);
    }
}

public class GetMyDashboardQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IPermissionEngine permissionEngine,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<GetMyDashboardQuery, ApiResponse<ResolvedDashboardDto>>
{
    public async Task<ApiResponse<ResolvedDashboardDto>> Handle(
        GetMyDashboardQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? 0;
        var tenantId = tenantContext.GetRequiredTenantId();
        if (userId <= 0)
            return ApiResponse<ResolvedDashboardDto>.FailResponse("Not authenticated.");

        using var connection = dbFactory.CreateConnection();
        if (!await DashboardBuilderQueries.TablesExistAsync(connection, cancellationToken))
            return ApiResponse<ResolvedDashboardDto>.FailResponse("Dashboard catalog is not available.");

        var profile = await connection.QuerySingleOrDefaultAsync<(
            string? DefaultDashboardKey, string? DefaultWorkspaceKey, string? RoleCode)>(new CommandDefinition("""
            SELECT u.DefaultDashboardKey, u.DefaultWorkspaceKey,
                   (SELECT TOP 1 r.Code FROM UserRoles ur
                    INNER JOIN Roles r ON r.Id = ur.RoleId
                    WHERE ur.UserId = u.Id ORDER BY r.Id) AS RoleCode
            FROM Users u
            WHERE u.Id = @UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0
            """,
            new { UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        string? workspaceDashboardKey = null;
        string? workspaceKey = profile.DefaultWorkspaceKey;
        try
        {
            var wsCatalog = await WorkspaceBuilderQueries.LoadCatalogAsync(connection, cancellationToken, activeOnly: true);
            var wsFlags = await WorkspaceBuilderQueries.LoadTenantFlagsAsync(connection, tenantId, cancellationToken);
            var resolved = WorkspaceBuilderQueries.Resolve(
                wsCatalog, wsFlags, profile.DefaultWorkspaceKey, null, profile.RoleCode);
            workspaceKey = resolved.Key;
            workspaceDashboardKey = resolved.DefaultDashboardKey;
        }
        catch
        {
            // Stage 10 optional
        }

        var eval = await permissionEngine.EvaluateAsync(userId, tenantId, cancellationToken);
        var permissions = eval.EffectivePermissions
            .Select(p => p.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modules = (await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var features = await MenuBuilderQueries.LoadTenantFeatureFlagsAsync(connection, tenantId, cancellationToken);

        // Default + Mobile → mobile layouts; pass audience=ERP for ERP shell.
        var preferMobile = !string.Equals(request.Audience, "ERP", StringComparison.OrdinalIgnoreCase);

        var resolvedDash = await DashboardBuilderQueries.ResolveForUserAsync(
            connection,
            profile.DefaultDashboardKey,
            workspaceDashboardKey,
            workspaceKey,
            profile.RoleCode,
            preferMobile,
            permissions,
            modules,
            features,
            cancellationToken);

        if (resolvedDash is null)
            return ApiResponse<ResolvedDashboardDto>.FailResponse("No dashboard resolved.");

        return ApiResponse<ResolvedDashboardDto>.SuccessResponse(resolvedDash);
    }
}

public class UpdateDashboardDefinitionCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDashboardDefinitionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateDashboardDefinitionCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await DashboardBuilderQueries.TablesExistAsync(connection, cancellationToken))
            return ApiResponse<bool>.FailResponse("Dashboard catalog is not available.");

        var p = request.Payload;
        if (string.IsNullOrWhiteSpace(p.DisplayName))
            return ApiResponse<bool>.FailResponse("Display name is required.");

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
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
                Key = request.Key,
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

        if (affected == 0)
            return ApiResponse<bool>.FailResponse("Dashboard not found.");

        _ = currentUser.UserId;
        return ApiResponse<bool>.SuccessResponse(true, "Dashboard updated.");
    }
}

public class UpdateDashboardLayoutCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateDashboardLayoutCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateDashboardLayoutCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        if (!await DashboardBuilderQueries.TablesExistAsync(connection, cancellationToken))
            return ApiResponse<bool>.FailResponse("Dashboard catalog is not available.");

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM DashboardDefinitions WHERE DashboardKey = @Key) THEN 1 ELSE 0 END",
            new { Key = request.Key },
            cancellationToken: cancellationToken));
        if (exists == 0)
            return ApiResponse<bool>.FailResponse("Dashboard not found.");

        var items = request.Payload.Items ?? Array.Empty<UpdateDashboardLayoutItemPayload>();
        if (items.Count == 0)
            return ApiResponse<bool>.FailResponse("Layout items are required.");

        var knownWidgets = (await DashboardBuilderQueries.LoadWidgetsAsync(connection, cancellationToken))
            .Select(w => w.WidgetKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!knownWidgets.Contains(item.WidgetKey))
                return ApiResponse<bool>.FailResponse($"Unknown widget key: {item.WidgetKey}");
        }

        using var tx = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM DashboardLayouts WHERE DashboardKey = @Key",
                new { Key = request.Key },
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
                        DashboardKey = request.Key,
                        item.WidgetKey,
                        item.SortOrder,
                        IsVisible = item.IsVisible
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
            }

            tx.Commit();
            return ApiResponse<bool>.SuccessResponse(true, "Layout updated.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
