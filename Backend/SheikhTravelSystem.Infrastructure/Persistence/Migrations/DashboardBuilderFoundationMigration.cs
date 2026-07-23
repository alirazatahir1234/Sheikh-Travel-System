using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 11 Dashboard Builder: DashboardDefinitions, WidgetDefinitions, Layouts.
/// </summary>
public static class DashboardBuilderFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DashboardDefinitions')
            CREATE TABLE DashboardDefinitions (
                DashboardKey NVARCHAR(100) NOT NULL CONSTRAINT PK_DashboardDefinitions PRIMARY KEY,
                DisplayName NVARCHAR(200) NOT NULL,
                Description NVARCHAR(500) NULL,
                Audience NVARCHAR(40) NOT NULL CONSTRAINT DF_DashboardDefinitions_Audience DEFAULT (N'Both'),
                DefaultWorkspaceKey NVARCHAR(100) NULL,
                Category NVARCHAR(100) NULL,
                SortOrder INT NOT NULL CONSTRAINT DF_DashboardDefinitions_SortOrder DEFAULT (0),
                Status NVARCHAR(40) NOT NULL CONSTRAINT DF_DashboardDefinitions_Status DEFAULT (N'Active'),
                Visible BIT NOT NULL CONSTRAINT DF_DashboardDefinitions_Visible DEFAULT (1),
                IsSystem BIT NOT NULL CONSTRAINT DF_DashboardDefinitions_IsSystem DEFAULT (1),
                IsActive BIT NOT NULL CONSTRAINT DF_DashboardDefinitions_IsActive DEFAULT (1),
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_DashboardDefinitions_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt DATETIME2 NULL
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DashboardWidgetDefinitions')
            CREATE TABLE DashboardWidgetDefinitions (
                WidgetKey NVARCHAR(100) NOT NULL CONSTRAINT PK_DashboardWidgetDefinitions PRIMARY KEY,
                DisplayName NVARCHAR(200) NOT NULL,
                Category NVARCHAR(100) NULL,
                Icon NVARCHAR(100) NULL,
                PermissionCode NVARCHAR(150) NULL,
                FeatureKey NVARCHAR(100) NULL,
                ModuleKey NVARCHAR(100) NULL,
                SupportsErp BIT NOT NULL CONSTRAINT DF_DashboardWidgetDefinitions_SupportsErp DEFAULT (1),
                SupportsMobile BIT NOT NULL CONSTRAINT DF_DashboardWidgetDefinitions_SupportsMobile DEFAULT (1),
                SortOrder INT NOT NULL CONSTRAINT DF_DashboardWidgetDefinitions_SortOrder DEFAULT (0),
                Status NVARCHAR(40) NOT NULL CONSTRAINT DF_DashboardWidgetDefinitions_Status DEFAULT (N'Active'),
                Visible BIT NOT NULL CONSTRAINT DF_DashboardWidgetDefinitions_Visible DEFAULT (1),
                IsActive BIT NOT NULL CONSTRAINT DF_DashboardWidgetDefinitions_IsActive DEFAULT (1),
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_DashboardWidgetDefinitions_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt DATETIME2 NULL
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DashboardLayouts')
            CREATE TABLE DashboardLayouts (
                DashboardKey NVARCHAR(100) NOT NULL,
                WidgetKey NVARCHAR(100) NOT NULL,
                SortOrder INT NOT NULL CONSTRAINT DF_DashboardLayouts_SortOrder DEFAULT (0),
                ColumnSpan INT NULL,
                IsVisible BIT NOT NULL CONSTRAINT DF_DashboardLayouts_IsVisible DEFAULT (1),
                CONSTRAINT PK_DashboardLayouts PRIMARY KEY (DashboardKey, WidgetKey),
                CONSTRAINT FK_DashboardLayouts_Definitions FOREIGN KEY (DashboardKey) REFERENCES DashboardDefinitions(DashboardKey),
                CONSTRAINT FK_DashboardLayouts_Widgets FOREIGN KEY (WidgetKey) REFERENCES DashboardWidgetDefinitions(WidgetKey)
            );

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_DashboardLayouts_WidgetKey' AND object_id = OBJECT_ID('DashboardLayouts'))
                CREATE INDEX IX_DashboardLayouts_WidgetKey ON DashboardLayouts (WidgetKey);
            """, cancellationToken: cancellationToken));

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken)) == 1)
        {
            foreach (var (code, desc) in new[]
                     {
                         (PlatformPermissions.DashboardsView, "View dashboard catalog and layouts"),
                         (PlatformPermissions.DashboardsManage, "Manage dashboard layouts and metadata")
                     })
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'Platform', @Code, @Desc);
                    """,
                    new { Code = code, Desc = desc },
                    cancellationToken: cancellationToken));
            }

            var codes = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT PermissionCode FROM Permissions", cancellationToken: cancellationToken))).ToList();
            await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
                connection, tenantId: 1, "SUPER_ADMIN", codes, cancellationToken);
            await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
                connection, tenantId: 1, "TENANT_ADMIN",
                TenantRolePermissionTemplates.TenantAdmin, cancellationToken);
        }

        foreach (var seed in DashboardRegistrySeed.Dashboards)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM DashboardDefinitions WHERE DashboardKey = @DashboardKey)
                    UPDATE DashboardDefinitions SET
                        DisplayName = @DisplayName,
                        Description = COALESCE(Description, @Description),
                        Audience = @Audience,
                        DefaultWorkspaceKey = COALESCE(DefaultWorkspaceKey, @DefaultWorkspaceKey),
                        Category = COALESCE(Category, @Category),
                        SortOrder = @SortOrder,
                        Visible = @Visible,
                        IsSystem = @IsSystem,
                        IsActive = 1,
                        Status = N'Active',
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE DashboardKey = @DashboardKey;
                ELSE
                    INSERT INTO DashboardDefinitions (
                        DashboardKey, DisplayName, Description, Audience, DefaultWorkspaceKey, Category,
                        SortOrder, Status, Visible, IsSystem, IsActive)
                    VALUES (
                        @DashboardKey, @DisplayName, @Description, @Audience, @DefaultWorkspaceKey, @Category,
                        @SortOrder, N'Active', @Visible, @IsSystem, 1);
                """,
                new
                {
                    seed.DashboardKey,
                    seed.DisplayName,
                    seed.Description,
                    seed.Audience,
                    seed.DefaultWorkspaceKey,
                    seed.Category,
                    seed.SortOrder,
                    Visible = seed.Visible,
                    IsSystem = seed.IsSystem
                },
                cancellationToken: cancellationToken));
        }

        foreach (var seed in DashboardRegistrySeed.Widgets)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM DashboardWidgetDefinitions WHERE WidgetKey = @WidgetKey)
                    UPDATE DashboardWidgetDefinitions SET
                        DisplayName = @DisplayName,
                        Category = COALESCE(Category, @Category),
                        Icon = COALESCE(Icon, @Icon),
                        PermissionCode = COALESCE(PermissionCode, @PermissionCode),
                        FeatureKey = COALESCE(FeatureKey, @FeatureKey),
                        ModuleKey = COALESCE(ModuleKey, @ModuleKey),
                        SupportsErp = @SupportsErp,
                        SupportsMobile = @SupportsMobile,
                        SortOrder = @SortOrder,
                        Visible = @Visible,
                        IsActive = 1,
                        Status = N'Active',
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE WidgetKey = @WidgetKey;
                ELSE
                    INSERT INTO DashboardWidgetDefinitions (
                        WidgetKey, DisplayName, Category, Icon, PermissionCode, FeatureKey, ModuleKey,
                        SupportsErp, SupportsMobile, SortOrder, Status, Visible, IsActive)
                    VALUES (
                        @WidgetKey, @DisplayName, @Category, @Icon, @PermissionCode, @FeatureKey, @ModuleKey,
                        @SupportsErp, @SupportsMobile, @SortOrder, N'Active', @Visible, 1);
                """,
                new
                {
                    seed.WidgetKey,
                    seed.DisplayName,
                    seed.Category,
                    seed.Icon,
                    seed.PermissionCode,
                    seed.FeatureKey,
                    seed.ModuleKey,
                    SupportsErp = seed.SupportsErp,
                    SupportsMobile = seed.SupportsMobile,
                    seed.SortOrder,
                    Visible = seed.Visible
                },
                cancellationToken: cancellationToken));
        }

        foreach (var seed in DashboardRegistrySeed.Layouts)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM DashboardLayouts WHERE DashboardKey = @DashboardKey AND WidgetKey = @WidgetKey)
                    UPDATE DashboardLayouts SET SortOrder = @SortOrder, IsVisible = 1
                    WHERE DashboardKey = @DashboardKey AND WidgetKey = @WidgetKey;
                ELSE
                    INSERT INTO DashboardLayouts (DashboardKey, WidgetKey, SortOrder, IsVisible)
                    VALUES (@DashboardKey, @WidgetKey, @SortOrder, 1);
                """,
                new { seed.DashboardKey, seed.WidgetKey, seed.SortOrder },
                cancellationToken: cancellationToken));
        }

        // Backfill WorkspaceDefinitions.DefaultDashboardKey when Stage 10 table exists.
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WorkspaceDefinitions SET DefaultDashboardKey = N'mobile.driver', UpdatedAt = SYSUTCDATETIME()
                WHERE WorkspaceKey = N'driver' AND (DefaultDashboardKey IS NULL OR LTRIM(RTRIM(DefaultDashboardKey)) = '');
                UPDATE WorkspaceDefinitions SET DefaultDashboardKey = N'mobile.fleet_ops', UpdatedAt = SYSUTCDATETIME()
                WHERE WorkspaceKey IN (N'fleet', N'drivers', N'trips')
                  AND (DefaultDashboardKey IS NULL OR LTRIM(RTRIM(DefaultDashboardKey)) = '');
                UPDATE WorkspaceDefinitions SET DefaultDashboardKey = N'mobile.admin', UpdatedAt = SYSUTCDATETIME()
                WHERE WorkspaceKey IN (N'company', N'platform', N'home')
                  AND (DefaultDashboardKey IS NULL OR LTRIM(RTRIM(DefaultDashboardKey)) = '');
                UPDATE WorkspaceDefinitions SET DefaultDashboardKey = N'erp.default', UpdatedAt = SYSUTCDATETIME()
                WHERE WorkspaceKey = N'finance'
                  AND (DefaultDashboardKey IS NULL OR LTRIM(RTRIM(DefaultDashboardKey)) = '');
                """, cancellationToken: cancellationToken));
        }
        catch
        {
            // Stage 10 may not be applied on older DBs.
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'platform')
            AND NOT EXISTS (
                SELECT 1 FROM PlatformMenus pm
                INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
                WHERE m.ModuleKey = N'platform' AND pm.Name = N'Dashboards')
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
            SELECT m.Id, NULL, N'Dashboards', N'/platform/dashboard-management', N'dashboard_customize',
                   N'Platform.Dashboards.Manage', 47, 1,
                   N'Dashboards', N'Dashboard catalog and layouts', N'Platform', 1, NULL, NULL, 0, SYSUTCDATETIME()
            FROM PlatformModules m WHERE m.ModuleKey = N'platform';
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "DashboardBuilderFoundationMigration applied ({Dashboards} dashboards, {Widgets} widgets, {Layouts} layout rows).",
            DashboardRegistrySeed.Dashboards.Count,
            DashboardRegistrySeed.Widgets.Count,
            DashboardRegistrySeed.Layouts.Count);
    }
}
