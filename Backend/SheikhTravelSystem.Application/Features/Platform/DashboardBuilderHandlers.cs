using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

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

}

public class GetDashboardCatalogQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetDashboardCatalogQuery, ApiResponse<IReadOnlyList<DashboardDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DashboardDefinitionDto>>> Handle(
        GetDashboardCatalogQuery request, CancellationToken cancellationToken)
    {
        if (!await platformRepository.DashboardTablesExistAsync(cancellationToken))
            return ApiResponse<IReadOnlyList<DashboardDefinitionDto>>.SuccessResponse([]);

        var rows = await platformRepository.LoadDashboardDefinitionsAsync(request.ActiveOnly, cancellationToken);
        var counts = await platformRepository.LoadDashboardWidgetCountsAsync(cancellationToken);
        var dtos = rows.Select(r => DashboardBuilderQueries.ToDefinitionDto(
            r, counts.GetValueOrDefault(r.DashboardKey))).ToList();
        return ApiResponse<IReadOnlyList<DashboardDefinitionDto>>.SuccessResponse(dtos);
    }
}

public class GetDashboardWidgetsQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetDashboardWidgetsQuery, ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>> Handle(
        GetDashboardWidgetsQuery request, CancellationToken cancellationToken)
    {
        if (!await platformRepository.DashboardTablesExistAsync(cancellationToken))
            return ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>.SuccessResponse([]);

        var rows = await platformRepository.LoadDashboardWidgetsAsync(request.ActiveOnly, cancellationToken);
        return ApiResponse<IReadOnlyList<DashboardWidgetDefinitionDto>>.SuccessResponse(
            rows.Select(DashboardBuilderQueries.ToWidgetDto).ToList());
    }
}

public class GetDashboardByKeyQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetDashboardByKeyQuery, ApiResponse<DashboardDetailDto>>
{
    public async Task<ApiResponse<DashboardDetailDto>> Handle(
        GetDashboardByKeyQuery request, CancellationToken cancellationToken)
    {
        if (!await platformRepository.DashboardTablesExistAsync(cancellationToken))
            return ApiResponse<DashboardDetailDto>.FailResponse("Dashboard catalog is not available.");

        var rows = await platformRepository.LoadDashboardDefinitionsAsync(cancellationToken: cancellationToken);
        var def = rows.FirstOrDefault(r =>
            r.DashboardKey.Equals(request.Key, StringComparison.OrdinalIgnoreCase));
        if (def is null)
            return ApiResponse<DashboardDetailDto>.FailResponse("Dashboard not found.");

        var layout = await platformRepository.LoadDashboardLayoutAsync(def.DashboardKey, cancellationToken: cancellationToken);
        var dto = new DashboardDetailDto(
            DashboardBuilderQueries.ToDefinitionDto(def, layout.Count(l => l.IsVisible)),
            layout.Select(DashboardBuilderQueries.ToLayoutItem).ToList());
        return ApiResponse<DashboardDetailDto>.SuccessResponse(dto);
    }
}

public class GetMyDashboardQueryHandler(
    IPlatformRepository platformRepository,
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

        if (!await platformRepository.DashboardTablesExistAsync(cancellationToken))
            return ApiResponse<ResolvedDashboardDto>.FailResponse("Dashboard catalog is not available.");

        var profile = await platformRepository.GetUserDashboardProfileAsync(userId, tenantId, cancellationToken);

        string? workspaceDashboardKey = null;
        string? workspaceKey = profile?.DefaultWorkspaceKey;
        try
        {
            var wsCatalog = await platformRepository.LoadWorkspaceCatalogAsync(activeOnly: true, cancellationToken);
            var wsFlags = await platformRepository.LoadTenantWorkspaceFlagsAsync(tenantId, cancellationToken);
            var resolved = WorkspaceBuilderQueries.Resolve(
                wsCatalog, wsFlags, profile?.DefaultWorkspaceKey, null, profile?.RoleCode);
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
        var features = await platformRepository.LoadTenantFeatureFlagsAsync(tenantId, cancellationToken);

        var preferMobile = !string.Equals(request.Audience, "ERP", StringComparison.OrdinalIgnoreCase);

        var resolvedDash = await platformRepository.ResolveDashboardForUserAsync(
            profile?.DefaultDashboardKey,
            workspaceDashboardKey,
            workspaceKey,
            profile?.RoleCode,
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
    IPlatformRepository platformRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDashboardDefinitionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateDashboardDefinitionCommand request, CancellationToken cancellationToken)
    {
        if (!await platformRepository.DashboardTablesExistAsync(cancellationToken))
            return ApiResponse<bool>.FailResponse("Dashboard catalog is not available.");

        var p = request.Payload;
        if (string.IsNullOrWhiteSpace(p.DisplayName))
            return ApiResponse<bool>.FailResponse("Display name is required.");

        var affected = await platformRepository.UpdateDashboardDefinitionAsync(request.Key, p, cancellationToken);
        if (affected == 0)
            return ApiResponse<bool>.FailResponse("Dashboard not found.");

        _ = currentUser.UserId;
        return ApiResponse<bool>.SuccessResponse(true, "Dashboard updated.");
    }
}

public class UpdateDashboardLayoutCommandHandler(IPlatformRepository platformRepository)
    : IRequestHandler<UpdateDashboardLayoutCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateDashboardLayoutCommand request, CancellationToken cancellationToken)
    {
        if (!await platformRepository.DashboardTablesExistAsync(cancellationToken))
            return ApiResponse<bool>.FailResponse("Dashboard catalog is not available.");

        if (!await platformRepository.DashboardExistsAsync(request.Key, cancellationToken))
            return ApiResponse<bool>.FailResponse("Dashboard not found.");

        var items = request.Payload.Items ?? Array.Empty<UpdateDashboardLayoutItemPayload>();
        if (items.Count == 0)
            return ApiResponse<bool>.FailResponse("Layout items are required.");

        var knownWidgets = (await platformRepository.LoadDashboardWidgetsAsync(cancellationToken: cancellationToken))
            .Select(w => w.WidgetKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!knownWidgets.Contains(item.WidgetKey))
                return ApiResponse<bool>.FailResponse($"Unknown widget key: {item.WidgetKey}");
        }

        await platformRepository.UpdateDashboardLayoutAsync(request.Key, items, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Layout updated.");
    }
}
