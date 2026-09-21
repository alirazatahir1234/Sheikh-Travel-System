using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds AI Management + MCP Console under the Administration sidebar module.
/// </summary>
public static class McpConsoleMenuMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus'
                ) THEN 1 ELSE 0 END
                """,
                cancellationToken: cancellationToken)) != 1)
            return;

        await EnsureAdminMenuAsync(
            connection,
            "AI Management",
            "/ai",
            "smart_toy",
            AiPermissions.View,
            20,
            cancellationToken);

        await EnsureAdminMenuAsync(
            connection,
            "MCP Console",
            "/mcp",
            "hub",
            AiPermissions.View,
            21,
            cancellationToken);

        logger.LogInformation(
            "McpConsoleMenuMigration applied (Administration → AI Management + MCP Console).");
    }

    private static async Task EnsureAdminMenuAsync(
        System.Data.IDbConnection connection,
        string name,
        string route,
        string icon,
        string permission,
        int sort,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE pm SET pm.Route = @Route, pm.Icon = @Icon, pm.PermissionCode = @Permission,
                pm.SortOrder = @Sort, pm.IsActive = 1, pm.Visible = 1,
                pm.DisplayName = @Name, pm.ModuleKey = N'administration',
                pm.UpdatedAt = SYSUTCDATETIME()
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = N'administration'
            WHERE pm.Name = @Name OR pm.Route = @Route;

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Visible, ModuleKey, UpdatedAt)
            SELECT m.Id, NULL, @Name, @Route, @Icon, @Permission, @Sort, 1,
                   @Name, 1, N'administration', SYSUTCDATETIME()
            FROM PlatformModules m
            WHERE m.ModuleKey = N'administration'
              AND NOT EXISTS (
                    SELECT 1 FROM PlatformMenus x
                    WHERE x.ModuleId = m.Id AND (x.Name = @Name OR x.Route = @Route)
              );
            """,
            new { Name = name, Route = route, Icon = icon, Permission = permission, Sort = sort },
            cancellationToken: ct));
    }
}
