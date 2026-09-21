using Dapper;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<(IReadOnlyList<MenuBuilderQueries.ModuleRow> Modules, IReadOnlyList<MenuBuilderQueries.MenuRow> Menus)> LoadNavTablesAsync(
        bool activeMenusOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var modules = (await connection.QueryAsync<MenuBuilderQueries.ModuleRow>(new CommandDefinition("""
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

            var menus = (await connection.QueryAsync<MenuBuilderQueries.MenuRow>(
                new CommandDefinition(menuSql, cancellationToken: cancellationToken))).ToList();

            return (modules, menus);
        }
        catch
        {
            var modules = (await connection.QueryAsync<MenuBuilderQueries.ModuleRow>(new CommandDefinition("""
                SELECT Id, Name, ModuleKey, Icon, SortOrder, IsCollapsible,
                       Name AS DisplayName, CAST(NULL AS NVARCHAR(500)) AS Description, CAST(1 AS BIT) AS Visible
                FROM PlatformModules
                ORDER BY SortOrder, Id
                """, cancellationToken: cancellationToken))).ToList();

            var menus = (await connection.QueryAsync<MenuBuilderQueries.MenuRow>(new CommandDefinition("""
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

    public async Task<bool> PlatformModuleExistsAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var moduleExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM PlatformModules WHERE Id = @ModuleId",
            new { ModuleId = moduleId },
            cancellationToken: cancellationToken));
        return moduleExists > 0;
    }

    public async Task<int> UpdateMenuModuleAsync(int id, UpdateMenuModulePayload p, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
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
                Id = id,
                p.DisplayName,
                p.Icon,
                p.SortOrder,
                p.Visible,
                p.IsCollapsible
            },
            cancellationToken: cancellationToken));
    }

    public async Task<int> UpdateMenuItemAsync(int id, UpdateMenuItemPayload p, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
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
                Id = id,
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
    }

    public async Task<int> CreateMenuItemAsync(CreateMenuItemPayload p, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var name = string.IsNullOrWhiteSpace(p.Name) ? "New Menu" : p.Name.Trim();
        var displayName = string.IsNullOrWhiteSpace(p.DisplayName) ? name : p.DisplayName!.Trim();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
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
    }

    public async Task<int> DeleteMenuItemAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PlatformMenus SET
                IsActive = 0,
                Visible = 0,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id;
            """,
            new { Id = id },
            cancellationToken: cancellationToken));
    }
}
