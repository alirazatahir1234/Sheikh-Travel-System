using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 9 Menu Builder: catalog metadata on PlatformMenus / PlatformModules.
/// </summary>
public static class MenuBuilderFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await AddColumnIfMissingAsync(connection, "PlatformMenus", "DisplayName", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformMenus", "Description", "NVARCHAR(500) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformMenus", "Category", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformMenus", "Visible", "BIT NOT NULL CONSTRAINT DF_PlatformMenus_Visible DEFAULT (1)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformMenus", "FeatureKey", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformMenus", "ModuleKey", "NVARCHAR(100) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformMenus", "IsMobileSupported", "BIT NOT NULL CONSTRAINT DF_PlatformMenus_IsMobileSupported DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformMenus", "UpdatedAt", "DATETIME2 NULL", cancellationToken);

        await AddColumnIfMissingAsync(connection, "PlatformModules", "DisplayName", "NVARCHAR(200) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformModules", "Description", "NVARCHAR(500) NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "PlatformModules", "Visible", "BIT NOT NULL CONSTRAINT DF_PlatformModules_Visible DEFAULT (1)", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PlatformMenus
            SET DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), Name)
            WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = '';

            UPDATE PlatformModules
            SET DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), Name)
            WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = '';
            """, cancellationToken: cancellationToken));

        foreach (var mod in MenuRegistrySeed.Modules)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PlatformModules SET
                    DisplayName = @DisplayName,
                    Description = COALESCE(Description, @Description),
                    Visible = @Visible
                WHERE ModuleKey = @ModuleKey;
                """,
                new
                {
                    mod.ModuleKey,
                    mod.DisplayName,
                    mod.Description,
                    Visible = mod.Visible
                },
                cancellationToken: cancellationToken));
        }

        var menus = (await connection.QueryAsync<(int Id, string Name, string? Route)>(new CommandDefinition(
            "SELECT Id, Name, Route FROM PlatformMenus",
            cancellationToken: cancellationToken))).ToList();

        var updated = 0;
        foreach (var menu in menus)
        {
            var seed = MenuRegistrySeed.FindByRouteOrName(menu.Route, menu.Name);
            if (seed is null) continue;

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PlatformMenus SET
                    DisplayName = @DisplayName,
                    Description = COALESCE(Description, @Description),
                    Category = COALESCE(Category, @Category),
                    FeatureKey = COALESCE(FeatureKey, @FeatureKey),
                    ModuleKey = COALESCE(ModuleKey, @ModuleKey),
                    IsMobileSupported = CASE WHEN IsMobileSupported = 1 THEN 1 ELSE @IsMobileSupported END,
                    Visible = COALESCE(Visible, 1),
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id;
                """,
                new
                {
                    menu.Id,
                    seed.DisplayName,
                    seed.Description,
                    seed.Category,
                    seed.FeatureKey,
                    seed.ModuleKey,
                    IsMobileSupported = seed.IsMobileSupported
                },
                cancellationToken: cancellationToken));
            updated++;
        }

        // Ensure Menu Management nav item exists under platform module.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'platform')
            AND NOT EXISTS (
                SELECT 1 FROM PlatformMenus pm
                INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
                WHERE m.ModuleKey = N'platform' AND pm.Name = N'Menus')
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
            SELECT m.Id, NULL, N'Menus', N'/platform/menu-management', N'menu',
                   N'Platform.Menus.Manage', 45, 1,
                   N'Menus', N'Navigation catalog', N'Platform', 1, NULL, NULL, 0, SYSUTCDATETIME()
            FROM PlatformModules m WHERE m.ModuleKey = N'platform';
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "MenuBuilderFoundationMigration applied ({MenuSeed} menu metadata rows, {ModuleSeed} module metadata rows).",
            updated, MenuRegistrySeed.Modules.Count);
    }

    private static async Task AddColumnIfMissingAsync(
        IDbConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column)
                ALTER TABLE [{table}] ADD [{column}] {definition};
            """,
            new { Table = table, Column = column },
            cancellationToken: cancellationToken));
    }
}
