using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Stage 9 Menu Builder: catalog load + runtime nav filtering helpers.</summary>
internal static class MenuBuilderQueries
{
    internal sealed record ModuleRow(
        int Id,
        string Name,
        string ModuleKey,
        string? Icon,
        int SortOrder,
        bool IsCollapsible,
        string? DisplayName,
        string? Description,
        bool Visible);

    internal sealed record MenuRow(
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

    public static async Task<(IReadOnlyList<ModuleRow> Modules, IReadOnlyList<MenuRow> Menus)> LoadNavTablesAsync(
        IDbConnection connection,
        CancellationToken cancellationToken,
        bool activeMenusOnly = false)
    {
        try
        {
            var modules = (await connection.QueryAsync<ModuleRow>(new CommandDefinition("""
                SELECT Id, Name, ModuleKey, Icon, SortOrder, IsCollapsible,
                       COALESCE(DisplayName, Name) AS DisplayName,
                       Description,
                       COALESCE(Visible, 1) AS Visible
                FROM PlatformModules
                ORDER BY SortOrder, Id
                """, cancellationToken: cancellationToken))).ToList();

            var menuSql = activeMenusOnly
                ? """
                  SELECT Id, ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                         COALESCE(DisplayName, Name) AS DisplayName, Description, Category,
                         COALESCE(Visible, 1) AS Visible, FeatureKey, ModuleKey,
                         COALESCE(IsMobileSupported, 0) AS IsMobileSupported
                  FROM PlatformMenus
                  WHERE IsActive = 1 AND COALESCE(Visible, 1) = 1
                  ORDER BY SortOrder, Id
                  """
                : """
                  SELECT Id, ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                         COALESCE(DisplayName, Name) AS DisplayName, Description, Category,
                         COALESCE(Visible, 1) AS Visible, FeatureKey, ModuleKey,
                         COALESCE(IsMobileSupported, 0) AS IsMobileSupported
                  FROM PlatformMenus
                  ORDER BY SortOrder, Id
                  """;

            var menus = (await connection.QueryAsync<MenuRow>(
                new CommandDefinition(menuSql, cancellationToken: cancellationToken))).ToList();

            return (modules, menus);
        }
        catch
        {
            // Pre-migration fallback (no metadata columns).
            var modules = (await connection.QueryAsync<ModuleRow>(new CommandDefinition("""
                SELECT Id, Name, ModuleKey, Icon, SortOrder, IsCollapsible,
                       Name AS DisplayName, CAST(NULL AS NVARCHAR(500)) AS Description, CAST(1 AS BIT) AS Visible
                FROM PlatformModules
                ORDER BY SortOrder, Id
                """, cancellationToken: cancellationToken))).ToList();

            var menus = (await connection.QueryAsync<MenuRow>(new CommandDefinition("""
                SELECT Id, ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                       Name AS DisplayName, CAST(NULL AS NVARCHAR(500)) AS Description,
                       CAST(NULL AS NVARCHAR(100)) AS Category, CAST(1 AS BIT) AS Visible,
                       CAST(NULL AS NVARCHAR(100)) AS FeatureKey, CAST(NULL AS NVARCHAR(100)) AS ModuleKey,
                       CAST(0 AS BIT) AS IsMobileSupported
                FROM PlatformMenus
                WHERE (@ActiveOnly = 0 OR IsActive = 1)
                ORDER BY SortOrder, Id
                """,
                new { ActiveOnly = activeMenusOnly ? 1 : 0 },
                cancellationToken: cancellationToken))).ToList();

            return (modules, menus);
        }
    }

    public static async Task<Dictionary<string, bool>> LoadTenantFeatureFlagsAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
        => await FeatureRegistryQueries.LoadTenantFeatureFlagsAsync(connection, tenantId, cancellationToken);

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
            "administration" => enabled.Any(k => k is "users" or "driver-allowance-rules"),
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

public class GetMenuCatalogQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetMenuCatalogQuery, ApiResponse<MenuCatalogDto>>
{
    public async Task<ApiResponse<MenuCatalogDto>> Handle(GetMenuCatalogQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var (modules, menus) = await MenuBuilderQueries.LoadNavTablesAsync(connection, cancellationToken);
        return ApiResponse<MenuCatalogDto>.SuccessResponse(MenuBuilderQueries.ToCatalog(modules, menus));
    }
}

public class UpdateMenuModuleCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateMenuModuleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMenuModuleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var p = request.Payload;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PlatformModules SET
                DisplayName = COALESCE(@DisplayName, DisplayName, Name),
                Icon = @Icon,
                SortOrder = @SortOrder,
                Visible = @Visible,
                IsCollapsible = @IsCollapsible
            WHERE Id = @Id;
            """,
            new
            {
                request.Id,
                p.DisplayName,
                p.Icon,
                p.SortOrder,
                p.Visible,
                p.IsCollapsible
            },
            cancellationToken: cancellationToken));

        return rows == 0
            ? ApiResponse<bool>.FailResponse("Menu module not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Menu module updated.");
    }
}

public class UpdateMenuItemCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateMenuItemCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMenuItemCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var p = request.Payload;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PlatformMenus SET
                DisplayName = COALESCE(@DisplayName, DisplayName, Name),
                Description = @Description,
                Category = @Category,
                Route = @Route,
                Icon = @Icon,
                PermissionCode = @PermissionCode,
                SortOrder = @SortOrder,
                IsActive = @IsActive,
                Visible = @Visible,
                FeatureKey = @FeatureKey,
                ModuleKey = @ModuleKey,
                IsMobileSupported = @IsMobileSupported,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id;
            """,
            new
            {
                request.Id,
                p.DisplayName,
                p.Description,
                p.Category,
                p.Route,
                p.Icon,
                p.PermissionCode,
                p.SortOrder,
                p.IsActive,
                p.Visible,
                p.FeatureKey,
                p.ModuleKey,
                p.IsMobileSupported
            },
            cancellationToken: cancellationToken));

        return rows == 0
            ? ApiResponse<bool>.FailResponse("Menu item not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Menu item updated.");
    }
}

public class CreateMenuItemCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateMenuItemCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateMenuItemCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var p = request.Payload;

        var moduleExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM PlatformModules WHERE Id = @ModuleId",
            new { p.ModuleId },
            cancellationToken: cancellationToken));
        if (moduleExists == 0)
            return ApiResponse<int>.FailResponse("Module not found.");

        var name = string.IsNullOrWhiteSpace(p.Name) ? "New Menu" : p.Name.Trim();
        var displayName = string.IsNullOrWhiteSpace(p.DisplayName) ? name : p.DisplayName!.Trim();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO PlatformMenus (
                ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (
                @ModuleId, NULL, @Name, @Route, @Icon, @PermissionCode, @SortOrder, 1,
                @DisplayName, @Description, @Category, @Visible, @FeatureKey, @ModuleKey, @IsMobileSupported, SYSUTCDATETIME());
            """,
            new
            {
                p.ModuleId,
                Name = name,
                DisplayName = displayName,
                p.Description,
                p.Category,
                p.Route,
                p.Icon,
                p.PermissionCode,
                p.SortOrder,
                p.Visible,
                p.FeatureKey,
                p.ModuleKey,
                p.IsMobileSupported
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Menu item created.");
    }
}

public class DeleteMenuItemCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteMenuItemCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        // Soft-deactivate preferred over hard delete for seeded items.
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PlatformMenus SET
                IsActive = 0,
                Visible = 0,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id;
            """,
            new { request.Id },
            cancellationToken: cancellationToken));

        return rows == 0
            ? ApiResponse<bool>.FailResponse("Menu item not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Menu item deactivated.");
    }
}
