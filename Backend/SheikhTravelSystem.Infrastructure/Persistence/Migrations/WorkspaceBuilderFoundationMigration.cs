using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 10 Workspace Builder: WorkspaceDefinitions + TenantWorkspaces catalog.
/// </summary>
public static class WorkspaceBuilderFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorkspaceDefinitions')
            CREATE TABLE WorkspaceDefinitions (
                WorkspaceKey NVARCHAR(100) NOT NULL CONSTRAINT PK_WorkspaceDefinitions PRIMARY KEY,
                DisplayName NVARCHAR(200) NOT NULL,
                Description NVARCHAR(500) NULL,
                Category NVARCHAR(100) NULL,
                Icon NVARCHAR(100) NULL,
                HomeRoute NVARCHAR(200) NOT NULL CONSTRAINT DF_WorkspaceDefinitions_HomeRoute DEFAULT (N'/dashboard'),
                SortOrder INT NOT NULL CONSTRAINT DF_WorkspaceDefinitions_SortOrder DEFAULT (0),
                Visible BIT NOT NULL CONSTRAINT DF_WorkspaceDefinitions_Visible DEFAULT (1),
                IsActive BIT NOT NULL CONSTRAINT DF_WorkspaceDefinitions_IsActive DEFAULT (1),
                IsMobileSupported BIT NOT NULL CONSTRAINT DF_WorkspaceDefinitions_IsMobileSupported DEFAULT (0),
                ModuleKeysJson NVARCHAR(MAX) NULL,
                FeatureKey NVARCHAR(100) NULL,
                DefaultDashboardKey NVARCHAR(100) NULL,
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WorkspaceDefinitions_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt DATETIME2 NULL
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantWorkspaces')
            CREATE TABLE TenantWorkspaces (
                TenantId INT NOT NULL,
                WorkspaceKey NVARCHAR(100) NOT NULL,
                IsEnabled BIT NOT NULL CONSTRAINT DF_TenantWorkspaces_IsEnabled DEFAULT (1),
                EnabledBy INT NULL,
                EnabledDate DATETIME2 NULL,
                LastModified DATETIME2 NULL,
                CONSTRAINT PK_TenantWorkspaces PRIMARY KEY (TenantId, WorkspaceKey),
                CONSTRAINT FK_TenantWorkspaces_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT FK_TenantWorkspaces_Definitions FOREIGN KEY (WorkspaceKey) REFERENCES WorkspaceDefinitions(WorkspaceKey)
            );

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_TenantWorkspaces_WorkspaceKey' AND object_id = OBJECT_ID('TenantWorkspaces'))
                CREATE INDEX IX_TenantWorkspaces_WorkspaceKey ON TenantWorkspaces (WorkspaceKey);
            """, cancellationToken: cancellationToken));

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken)) == 1)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (N'Platform', @Code, N'Manage workspace catalog and company enablement');
                """,
                new { Code = PlatformPermissions.WorkspacesManage },
                cancellationToken: cancellationToken));

            var codes = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT PermissionCode FROM Permissions", cancellationToken: cancellationToken))).ToList();
            await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
                connection, tenantId: 1, "SUPER_ADMIN", codes, cancellationToken);
            await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
                connection, tenantId: 1, "TENANT_ADMIN",
                TenantRolePermissionTemplates.TenantAdmin, cancellationToken);
        }

        foreach (var seed in WorkspaceRegistrySeed.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM WorkspaceDefinitions WHERE WorkspaceKey = @WorkspaceKey)
                    UPDATE WorkspaceDefinitions SET
                        DisplayName = @DisplayName,
                        Description = COALESCE(Description, @Description),
                        Category = COALESCE(Category, @Category),
                        Icon = COALESCE(Icon, @Icon),
                        HomeRoute = COALESCE(NULLIF(LTRIM(RTRIM(HomeRoute)), ''), @HomeRoute),
                        SortOrder = @SortOrder,
                        Visible = @Visible,
                        IsActive = 1,
                        IsMobileSupported = CASE WHEN IsMobileSupported = 1 THEN 1 ELSE @IsMobileSupported END,
                        ModuleKeysJson = COALESCE(ModuleKeysJson, @ModuleKeysJson),
                        FeatureKey = COALESCE(FeatureKey, @FeatureKey),
                        DefaultDashboardKey = COALESCE(DefaultDashboardKey, @DefaultDashboardKey),
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE WorkspaceKey = @WorkspaceKey;
                ELSE
                    INSERT INTO WorkspaceDefinitions (
                        WorkspaceKey, DisplayName, Description, Category, Icon, HomeRoute, SortOrder,
                        Visible, IsActive, IsMobileSupported, ModuleKeysJson, FeatureKey, DefaultDashboardKey)
                    VALUES (
                        @WorkspaceKey, @DisplayName, @Description, @Category, @Icon, @HomeRoute, @SortOrder,
                        @Visible, 1, @IsMobileSupported, @ModuleKeysJson, @FeatureKey, @DefaultDashboardKey);
                """,
                new
                {
                    seed.WorkspaceKey,
                    seed.DisplayName,
                    seed.Description,
                    seed.Category,
                    seed.Icon,
                    seed.HomeRoute,
                    seed.SortOrder,
                    Visible = seed.Visible,
                    IsMobileSupported = seed.IsMobileSupported,
                    seed.ModuleKeysJson,
                    seed.FeatureKey,
                    seed.DefaultDashboardKey
                },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantWorkspaces (TenantId, WorkspaceKey, IsEnabled, EnabledDate, LastModified)
            SELECT t.Id, w.WorkspaceKey, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
            FROM Tenants t
            CROSS JOIN WorkspaceDefinitions w
            WHERE w.IsActive = 1 AND w.Visible = 1
              AND NOT EXISTS (
                SELECT 1 FROM TenantWorkspaces tw
                WHERE tw.TenantId = t.Id AND tw.WorkspaceKey = w.WorkspaceKey);
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'platform')
            AND NOT EXISTS (
                SELECT 1 FROM PlatformMenus pm
                INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
                WHERE m.ModuleKey = N'platform' AND pm.Name = N'Workspaces')
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
            SELECT m.Id, NULL, N'Workspaces', N'/platform/workspace-management', N'workspaces',
                   N'Platform.Workspaces.Manage', 46, 1,
                   N'Workspaces', N'Workspace catalog and company enablement', N'Platform', 1, NULL, NULL, 0, SYSUTCDATETIME()
            FROM PlatformModules m WHERE m.ModuleKey = N'platform';
            """, cancellationToken: cancellationToken));

        // Backfill DisplayName if MenuBuilder columns exist but insert path skipped them.
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PlatformMenus SET
                    DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), ''), Name),
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Name = N'Workspaces' AND (DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = '');
                """, cancellationToken: cancellationToken));
        }
        catch
        {
            // Older DBs without Stage 9 columns — nav insert above may have failed on extra cols.
        }

        logger.LogInformation(
            "WorkspaceBuilderFoundationMigration applied ({Count} workspace definitions).",
            WorkspaceRegistrySeed.All.Count);
    }
}
