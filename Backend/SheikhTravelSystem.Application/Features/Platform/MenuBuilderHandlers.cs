using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Stage 9 Menu Builder: catalog load + runtime nav filtering helpers.</summary>
public static class MenuBuilderQueries
{
    public sealed record ModuleRow(
        int Id,
        string Name,
        string ModuleKey,
        string? Icon,
        int SortOrder,
        bool IsCollapsible,
        string? DisplayName,
        string? Description,
        bool Visible);

    public sealed record MenuRow(
        int Id,
        int ModuleId,
        int? ParentId,
        string Name,
        string? Route,
        string? Icon,
        string? PermissionCode,
        int SortOrder,
        bool IsActive,
        string? DisplayName,
        string? Description,
        string? Category,
        bool Visible,
        string? FeatureKey,
        string? ModuleKey,
        bool IsMobileSupported);

    public static bool PassesFeatureGate(string? featureKey, IReadOnlyDictionary<string, bool> featureFlags)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) return true;
        if (featureFlags.Count == 0) return true; // soft: no tenant feature rows → pass
        if (!featureFlags.TryGetValue(featureKey, out var enabled)) return true; // unmapped → pass
        return enabled;
    }

    public static bool IsMenuItemEnabledByModule(string? permissionCode, IReadOnlyList<string> enabled)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return true;
        if (permissionCode.StartsWith("GPS.", StringComparison.OrdinalIgnoreCase))
            return enabled.Contains("gps-tracking", StringComparer.OrdinalIgnoreCase);
        return true;
    }

    public static bool IsNavModuleEnabled(string moduleKey, IReadOnlyList<string> enabled) =>
        moduleKey switch
        {
            "dashboard" => enabled.Contains("dashboard", StringComparer.OrdinalIgnoreCase),
            "operations" => enabled.Any(k => k is "bookings" or "routes"),
            "fleet" => enabled.Any(k => k is "vehicles" or "drivers" or "gps-tracking" or "fuel-logs" or "maintenance"),
            "customers" => enabled.Contains("customers", StringComparer.OrdinalIgnoreCase),
            "finance" => enabled.Contains("payments", StringComparer.OrdinalIgnoreCase),
            "analytics" => enabled.Any(k => k is "reports" or "audit-logs"),
            "administration" => enabled.Any(k => k is "users" or "driver-allowance-rules" or "dashboard"),
            "organization" => enabled.Any(k => k is "users" or "driver-allowance-rules" or "organization" or "platform"),
            "access_control" => enabled.Any(k => k is "users" or "driver-allowance-rules" or "access_control"),
            "platform" => true,
            _ => true
        };

    public static string Slugify(string value) =>
        value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('&', '-');

    public static List<MenuModuleDto> BuildUserMenu(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<MenuRow> menus,
        IReadOnlySet<string> permissionSet,
        IReadOnlyList<string> enabledModules,
        IReadOnlyDictionary<string, bool> featureFlags,
        IReadOnlyList<string>? workspaceModuleKeys = null)
    {
        var focusKeys = workspaceModuleKeys?
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var softFocus = focusKeys is { Count: > 0 };

        var result = new List<MenuModuleDto>();
        foreach (var module in modules)
        {
            if (!module.Visible) continue;
            if (enabledModules.Count > 0 && !IsNavModuleEnabled(module.ModuleKey, enabledModules))
                continue;
            // Soft workspace focus: hide modules outside the workspace ModuleKeys when list is non-empty.
            if (softFocus && !focusKeys!.Contains(module.ModuleKey, StringComparer.OrdinalIgnoreCase)
                && !string.Equals(module.ModuleKey, "platform", StringComparison.OrdinalIgnoreCase))
                continue;

            var items = menus
                .Where(m => m.ModuleId == module.Id && m.IsActive && m.Visible)
                .Where(m => string.IsNullOrEmpty(m.PermissionCode) || permissionSet.Contains(m.PermissionCode))
                .Where(m => IsMenuItemEnabledByModule(m.PermissionCode, enabledModules))
                .Where(m => PassesFeatureGate(m.FeatureKey, featureFlags))
                .Select(m =>
                {
                    var label = string.IsNullOrWhiteSpace(m.DisplayName) ? m.Name : m.DisplayName!;
                    return new MenuItemDto(
                        Slugify(m.Name),
                        label,
                        m.Icon ?? "circle",
                        m.Route ?? "/dashboard",
                        m.PermissionCode,
                        m.SortOrder,
                        m.DisplayName,
                        m.Description,
                        m.Category,
                        m.FeatureKey,
                        m.ModuleKey,
                        m.IsMobileSupported,
                        m.Visible);
                })
                .ToList();

            if (items.Count == 0) continue;

            var moduleLabel = string.IsNullOrWhiteSpace(module.DisplayName) ? module.Name : module.DisplayName!;
            result.Add(new MenuModuleDto(
                module.ModuleKey,
                moduleLabel,
                module.Icon ?? "folder",
                module.IsCollapsible,
                module.SortOrder,
                items,
                module.DisplayName,
                module.Description,
                module.Visible));
        }

        if (softFocus && result.Count > 1)
        {
            result = result
                .OrderBy(m =>
                {
                    var idx = focusKeys!.FindIndex(k =>
                        string.Equals(k, m.Id, StringComparison.OrdinalIgnoreCase));
                    return idx < 0 ? int.MaxValue : idx;
                })
                .ThenBy(m => m.SortOrder)
                .ToList();
        }

        return result;
    }

    public static CompanyNavSummaryDto ToNavSummary(IReadOnlyList<MenuModuleDto> menu)
    {
        var top = menu.Take(6)
            .Select(m => m.DisplayName ?? m.Label)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        var mobile = menu
            .SelectMany(m => m.Items)
            .Where(i => i.IsMobileSupported)
            .Select(i => i.DisplayName ?? i.Label)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        var itemCount = menu.Sum(m => m.Items.Count);
        return new CompanyNavSummaryDto(menu.Count, itemCount, top, mobile);
    }

    public static MenuCatalogDto ToCatalog(
        IReadOnlyList<ModuleRow> modules,
        IReadOnlyList<MenuRow> menus)
    {
        var catalogModules = modules.Select(module =>
        {
            var items = menus
                .Where(m => m.ModuleId == module.Id)
                .Select(m => new MenuCatalogItemDto(
                    m.Id,
                    m.ModuleId,
                    m.Name,
                    string.IsNullOrWhiteSpace(m.DisplayName) ? m.Name : m.DisplayName!,
                    m.Description,
                    m.Category,
                    m.Route,
                    m.Icon,
                    m.PermissionCode,
                    m.SortOrder,
                    m.IsActive,
                    m.Visible,
                    m.FeatureKey,
                    m.ModuleKey,
                    m.IsMobileSupported,
                    m.ParentId))
                .ToList();

            return new MenuCatalogModuleDto(
                module.Id,
                module.ModuleKey,
                module.Name,
                string.IsNullOrWhiteSpace(module.DisplayName) ? module.Name : module.DisplayName!,
                module.Description,
                module.Icon,
                module.SortOrder,
                module.IsCollapsible,
                module.Visible,
                items);
        }).ToList();

        return new MenuCatalogDto(catalogModules);
    }
}

public class GetMenuCatalogQueryHandler(IPlatformRepository platformRepository)
    : IRequestHandler<GetMenuCatalogQuery, ApiResponse<MenuCatalogDto>>
{
    public async Task<ApiResponse<MenuCatalogDto>> Handle(GetMenuCatalogQuery request, CancellationToken cancellationToken)
    {
        var (modules, menus) = await platformRepository.LoadNavTablesAsync(cancellationToken: cancellationToken);
        return ApiResponse<MenuCatalogDto>.SuccessResponse(MenuBuilderQueries.ToCatalog(modules, menus));
    }
}

public class UpdateMenuModuleCommandHandler(IPlatformRepository platformRepository)
    : IRequestHandler<UpdateMenuModuleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMenuModuleCommand request, CancellationToken cancellationToken)
    {
        var rows = await platformRepository.UpdateMenuModuleAsync(request.Id, request.Payload, cancellationToken);
        return rows == 0
            ? ApiResponse<bool>.FailResponse("Menu module not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Menu module updated.");
    }
}

public class UpdateMenuItemCommandHandler(IPlatformRepository platformRepository)
    : IRequestHandler<UpdateMenuItemCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMenuItemCommand request, CancellationToken cancellationToken)
    {
        var rows = await platformRepository.UpdateMenuItemAsync(request.Id, request.Payload, cancellationToken);
        return rows == 0
            ? ApiResponse<bool>.FailResponse("Menu item not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Menu item updated.");
    }
}

public class CreateMenuItemCommandHandler(IPlatformRepository platformRepository)
    : IRequestHandler<CreateMenuItemCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateMenuItemCommand request, CancellationToken cancellationToken)
    {
        var p = request.Payload;
        if (!await platformRepository.PlatformModuleExistsAsync(p.ModuleId, cancellationToken))
            return ApiResponse<int>.FailResponse("Module not found.");

        var id = await platformRepository.CreateMenuItemAsync(p, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Menu item created.");
    }
}

public class DeleteMenuItemCommandHandler(IPlatformRepository platformRepository)
    : IRequestHandler<DeleteMenuItemCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        var rows = await platformRepository.DeleteMenuItemAsync(request.Id, cancellationToken);
        return rows == 0
            ? ApiResponse<bool>.FailResponse("Menu item not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Menu item deactivated.");
    }
}
