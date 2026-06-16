using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Platform Foundation: tenants, org structure, RBAC, dynamic menus.
/// </summary>
public static class PlatformSchemaMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await ExtendTenantsAsync(connection, cancellationToken);
        await CreateBranchesAsync(connection, cancellationToken);
        await ExtendBranchesAsync(connection, cancellationToken);
        await CreateDepartmentsAsync(connection, cancellationToken);
        await ExtendDepartmentsAsync(connection, cancellationToken);
        await ExtendUsersAsync(connection, cancellationToken);
        await CreateRolesAsync(connection, cancellationToken);
        await CreatePermissionsAsync(connection, cancellationToken);
        await CreateRolePermissionsAsync(connection, cancellationToken);
        await CreateUserRolesAsync(connection, cancellationToken);
        await CreatePlatformModulesAsync(connection, cancellationToken);
        await CreatePlatformMenusAsync(connection, cancellationToken);

        await SeedPermissionsAsync(connection, cancellationToken);
        await SeedRolesAsync(connection, cancellationToken);
        await SeedRolePermissionsAsync(connection, cancellationToken);
        await SeedUserRolesFromLegacyAsync(connection, cancellationToken);
        await SeedPlatformModulesAsync(connection, cancellationToken);
        await SeedPlatformMenusAsync(connection, cancellationToken);
        await SeedDefaultBranchAndDepartmentsAsync(connection, cancellationToken);
        await SeedSuperAdminPermissionsAsync(connection, cancellationToken);
        await EnsureOrganizationAndAccessMenusAsync(connection, cancellationToken);
        await EnsureFleetMenusAsync(connection, cancellationToken);
        await EnsurePlatformAdminMenusAsync(connection, cancellationToken);
        await DeduplicatePlatformMenusAsync(connection, cancellationToken);
        await EnsurePlatformMenuUniqueIndexAsync(connection, cancellationToken);
        await EnsureDefaultSuperAdminAsync(connection, cancellationToken);

        logger.LogInformation("Platform schema migration completed.");
    }

    private static async Task ExtendTenantsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "Tenants", "Code", "NVARCHAR(50) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Tenants", "SubscriptionPlan", "NVARCHAR(100) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Tenants", "UpdatedAt", "DATETIME2 NULL", ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Tenants SET Code = UPPER(REPLACE(Slug, '-', '_')) WHERE Code IS NULL OR Code = '';
            UPDATE Tenants SET SubscriptionPlan = N'Enterprise' WHERE SubscriptionPlan IS NULL OR LTRIM(RTRIM(SubscriptionPlan)) = '';
            UPDATE Tenants SET Code = N'SK-' + RIGHT(N'0000' + CAST(Id AS NVARCHAR(10)), 4)
            WHERE Code IS NULL OR Code = '' OR Code NOT LIKE N'SK-%';
            """, cancellationToken: ct));
    }

    private static async Task CreateBranchesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Branches')
            CREATE TABLE Branches (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                Name NVARCHAR(200) NOT NULL,
                Address NVARCHAR(500) NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CONSTRAINT FK_Branches_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));

        await CreateIndexIfMissingAsync(connection, "IX_Branches_TenantId", "Branches", "TenantId", ct);
    }

    private static async Task ExtendBranchesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "Branches", "ParentBranchId", "INT NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "BranchCode", "NVARCHAR(50) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "BranchType", "NVARCHAR(50) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "BranchManagerUserId", "INT NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "Phone", "NVARCHAR(50) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "Email", "NVARCHAR(200) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "City", "NVARCHAR(100) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "Country", "NVARCHAR(100) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "TimeZone", "NVARCHAR(100) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "CurrencyCode", "NVARCHAR(10) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "Status", "INT NOT NULL DEFAULT 1", ct);
        await AddColumnIfMissingAsync(connection, "Branches", "IsGpsEnabled", "BIT NOT NULL DEFAULT 1", ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Branches SET BranchCode = 'HQ-' + RIGHT('000' + CAST(Id AS NVARCHAR(10)), 3)
            WHERE BranchCode IS NULL OR LTRIM(RTRIM(BranchCode)) = '';

            UPDATE Branches SET Status = CASE WHEN IsActive = 1 THEN 1 ELSE 2 END
            WHERE Status IS NULL OR Status NOT IN (1, 2, 3, 4);

            UPDATE Branches SET IsGpsEnabled = 1 WHERE IsGpsEnabled IS NULL;
            """, cancellationToken: ct));

        await CreateUniqueIndexIfMissingAsync(connection, "UQ_Branches_Tenant_Code", "Branches", "TenantId, BranchCode", ct);
        await CreateIndexIfMissingAsync(connection, "IX_Branches_ParentBranchId", "Branches", "ParentBranchId", ct);
    }

    private static async Task CreateDepartmentsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Departments')
            CREATE TABLE Departments (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CONSTRAINT FK_Departments_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));

        await CreateIndexIfMissingAsync(connection, "IX_Departments_TenantId", "Departments", "TenantId", ct);
    }

    private static async Task ExtendDepartmentsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "Departments", "DepartmentHeadUserId", "INT NULL", ct);
        await CreateIndexIfMissingAsync(connection, "IX_Departments_HeadUserId", "Departments", "DepartmentHeadUserId", ct);
    }

    private static async Task ExtendUsersAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "Users", "BranchId", "INT NULL", ct);
        await AddColumnIfMissingAsync(connection, "Users", "DepartmentId", "INT NULL", ct);
    }

    private static async Task CreateRolesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Roles')
            CREATE TABLE Roles (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                Code NVARCHAR(100) NOT NULL,
                IsSystem BIT NOT NULL DEFAULT 0,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CONSTRAINT FK_Roles_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT UQ_Roles_Tenant_Code UNIQUE (TenantId, Code)
            );
            """, cancellationToken: ct));

        await CreateIndexIfMissingAsync(connection, "IX_Roles_TenantId", "Roles", "TenantId", ct);
    }

    private static async Task CreatePermissionsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
            CREATE TABLE Permissions (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                ModuleName NVARCHAR(100) NOT NULL,
                PermissionCode NVARCHAR(200) NOT NULL,
                Description NVARCHAR(500) NULL,
                CONSTRAINT UQ_Permissions_Code UNIQUE (PermissionCode)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateRolePermissionsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RolePermissions')
            CREATE TABLE RolePermissions (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                RoleId INT NOT NULL,
                PermissionId INT NOT NULL,
                CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE,
                CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES Permissions(Id) ON DELETE CASCADE,
                CONSTRAINT UQ_RolePermissions UNIQUE (RoleId, PermissionId)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateUserRolesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserRoles')
            CREATE TABLE UserRoles (
                UserId INT NOT NULL,
                RoleId INT NOT NULL,
                PRIMARY KEY (UserId, RoleId),
                CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
            );
            """, cancellationToken: ct));
    }

    private static async Task CreatePlatformModulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformModules')
            CREATE TABLE PlatformModules (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                ModuleKey NVARCHAR(100) NOT NULL,
                Icon NVARCHAR(100) NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                IsCollapsible BIT NOT NULL DEFAULT 1,
                CONSTRAINT UQ_PlatformModules_Key UNIQUE (ModuleKey)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreatePlatformMenusAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus')
            CREATE TABLE PlatformMenus (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                ModuleId INT NOT NULL,
                ParentId INT NULL,
                Name NVARCHAR(200) NOT NULL,
                Route NVARCHAR(500) NULL,
                Icon NVARCHAR(100) NULL,
                PermissionCode NVARCHAR(200) NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT NOT NULL DEFAULT 1,
                CONSTRAINT FK_PlatformMenus_Modules FOREIGN KEY (ModuleId) REFERENCES PlatformModules(Id),
                CONSTRAINT FK_PlatformMenus_Parent FOREIGN KEY (ParentId) REFERENCES PlatformMenus(Id)
            );
            """, cancellationToken: ct));

        await CreateIndexIfMissingAsync(connection, "IX_PlatformMenus_ModuleId", "PlatformMenus", "ModuleId", ct);
    }

    private static async Task SeedPermissionsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var permissions = new (string Module, string Code, string Desc)[]
        {
            ("Platform", "Platform.Dashboard.View", "View dashboard"),
            ("Platform", "Platform.Users.View", "View users"),
            ("Platform", "Platform.Users.Create", "Create users"),
            ("Platform", "Platform.Users.Edit", "Edit users"),
            ("Platform", "Platform.Roles.View", "View roles"),
            ("Platform", "Platform.Roles.Manage", "Manage roles and permissions"),
            ("Platform", "Platform.Tenants.View", "View tenants"),
            ("Platform", "Platform.Tenants.Manage", "Manage tenants"),
            ("Platform", "Platform.Branches.Manage", "Manage branches"),
            ("Platform", "Platform.Departments.Manage", "Manage departments"),
            ("Platform", "Platform.AuditLogs.View", "View audit logs"),
            ("Platform", "Platform.Menus.Manage", "Manage navigation menus"),
            ("Platform", "Platform.Settings.View", "View platform settings"),
            ("Platform", "Platform.Settings.Manage", "Manage platform settings"),
            ("Operations", "Booking.View", "View bookings"),
            ("Operations", "Booking.Create", "Create bookings"),
            ("Operations", "Trip.View", "View trips"),
            ("Operations", "Route.View", "View routes"),
            ("Fleet", "Vehicle.View", "View vehicles"),
            ("Fleet", "Vehicle.Create", "Create vehicles"),
            ("Fleet", "Vehicle.Update", "Update vehicles"),
            ("Fleet", "Driver.View", "View drivers"),
            ("Fleet", "Driver.Manage", "Manage drivers"),
            ("Fleet", "GPS.View", "View GPS tracking"),
            ("Fleet", "Fuel.View", "View fuel logs"),
            ("Fleet", "Maintenance.View", "View maintenance"),
            ("Customers", "Customer.View", "View customers"),
            ("Finance", "Payment.View", "View payments"),
            ("Finance", "Invoice.View", "View invoices"),
            ("Analytics", "Report.View", "View reports"),
        };

        foreach (var (module, code, desc) in permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (@Module, @Code, @Desc);
                """, new { Module = module, Code = code, Desc = desc }, cancellationToken: ct));
        }
    }

    private static async Task SeedRolesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var tenantIds = (await connection.QueryAsync<int>(
            new CommandDefinition("SELECT Id FROM Tenants", cancellationToken: ct))).ToList();

        var roleDefs = new (string Name, string Code)[]
        {
            ("Super Admin", "SUPER_ADMIN"),
            ("Tenant Admin", "TENANT_ADMIN"),
            ("Fleet Manager", "FLEET_MANAGER"),
            ("Dispatcher", "DISPATCHER"),
            ("Driver Manager", "DRIVER_MANAGER"),
            ("Accountant", "ACCOUNTANT"),
            ("Driver", "DRIVER"),
        };

        foreach (var tenantId in tenantIds)
        {
            foreach (var (name, code) in roleDefs)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code)
                    INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                    VALUES (@TenantId, @Name, @Code, 1, 1, GETUTCDATE());
                    """, new { TenantId = tenantId, Name = name, Code = code }, cancellationToken: ct));
            }
        }
    }

    private static async Task SeedRolePermissionsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        foreach (var (roleCode, permissions) in TenantRolePermissionTemplates.StandardRoles)
        {
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, roleCode, permissions, ct);
        }
    }

    private static async Task SeedSuperAdminPermissionsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var codes = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PermissionCode FROM Permissions", cancellationToken: ct))).ToList();

        await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
            connection, tenantId: 1, "SUPER_ADMIN", codes, ct);
    }

    private static async Task EnsureOrganizationAndAccessMenusAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = 'organization')
            INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
            VALUES (N'Organization', 'organization', 'corporate_fare', 7, 1);

            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = 'access_control')
            INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
            VALUES (N'Access Control', 'access_control', 'admin_panel_settings', 8, 1);
            """, cancellationToken: ct));

        var orgMenus = new (string Name, string Route, string Icon, string Permission, int Sort)[]
        {
            ("Tenants", "/platform/tenants", "business", "Platform.Tenants.View", 0),
            ("Branches", "/platform/branches", "account_tree", "Platform.Branches.Manage", 1),
            ("Departments", "/platform/departments", "domain", "Platform.Departments.Manage", 2),
        };

        var accessMenus = new (string Name, string Route, string Icon, string Permission, int Sort)[]
        {
            ("Users", "/users", "manage_accounts", "Platform.Users.View", 0),
            ("Roles", "/platform/roles", "security", "Platform.Roles.View", 1),
            ("Allowance Rules", "/driver-allowance-rules", "rule", "Platform.Roles.Manage", 2),
        };

        foreach (var (name, route, icon, permission, sort) in orgMenus)
            await UpsertMenuUnderModuleAsync(connection, "organization", name, route, icon, permission, sort, ct);

        foreach (var (name, route, icon, permission, sort) in accessMenus)
            await UpsertMenuUnderModuleAsync(connection, "access_control", name, route, icon, permission, sort, ct);

        await UpsertMenuUnderModuleAsync(connection, "administration", "Settings", "/settings", "tune", "Platform.Settings.View", 10, ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.IsActive = 0
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
            WHERE m.ModuleKey = 'administration'
              AND pm.Name IN (N'Tenants', N'Users', N'Roles', N'Branches', N'Departments', N'Allowance Rules');
            """, cancellationToken: ct));
    }

    private static async Task UpsertMenuUnderModuleAsync(
        System.Data.IDbConnection connection,
        string moduleKey,
        string name,
        string route,
        string icon,
        string permission,
        int sort,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.Route = @Route, pm.Icon = @Icon, pm.PermissionCode = @Permission,
                pm.SortOrder = @Sort, pm.IsActive = 1
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId AND m.ModuleKey = @ModuleKey
            WHERE pm.Name = @Name;

            IF @@ROWCOUNT = 0
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive)
            SELECT m.Id, NULL, @Name, @Route, @Icon, @Permission, @Sort, 1
            FROM PlatformModules m
            WHERE m.ModuleKey = @ModuleKey
              AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.ModuleId = m.Id AND x.Name = @Name);
            """, new { ModuleKey = moduleKey, Name = name, Route = route, Icon = icon, Permission = permission, Sort = sort },
            cancellationToken: ct));
    }

    private static async Task EnsureFleetMenusAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PlatformModules SET Name = N'Fleet Management' WHERE ModuleKey = 'fleet';
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.Name = N'Fuel', pm.SortOrder = 8, pm.IsActive = 1
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
            WHERE m.ModuleKey = 'fleet' AND pm.Name = N'Fuel Logs';
            """, cancellationToken: ct));

        var fleetMenus = new (string Name, string Route, string Icon, string Permission, int Sort)[]
        {
            ("Vehicles", "/vehicles", "directions_bus", "Vehicle.View", 1),
            ("Drivers", "/drivers", "badge", "Driver.View", 2),
            ("Assignments", "/fleet/assignments", "event_available", "Vehicle.View", 3),
            ("GPS Tracking", "/gps-tracking", "my_location", "GPS.View", 4),
            ("Maintenance", "/maintenance", "build", "Maintenance.View", 5),
            ("Inspection", "/fleet/inspections", "fact_check", "Vehicle.View", 6),
            ("Compliance", "/fleet/compliance", "verified_user", "Vehicle.View", 7),
            ("Fuel", "/fuel-logs", "local_gas_station", "Fuel.View", 8),
        };

        foreach (var (name, route, icon, permission, sort) in fleetMenus)
            await UpsertMenuUnderModuleAsync(connection, "fleet", name, route, icon, permission, sort, ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE pm SET pm.IsActive = 0
            FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
            WHERE m.ModuleKey = 'fleet'
              AND pm.Name IN (N'Fuel Logs', N'Inspections');
            """, cancellationToken: ct));
    }

    private static async Task EnsurePlatformAdminMenusAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var orgMenuCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM PlatformMenus pm
            INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
            WHERE m.ModuleKey = 'organization' AND pm.IsActive = 1
            """, cancellationToken: ct));
        if (orgMenuCount > 0) return;

        var adminMenus = new (string Name, string Route, string Icon, string Permission, int Sort)[]
        {
            ("Tenants", "/platform/tenants", "business", "Platform.Tenants.View", 0),
            ("Users", "/users", "manage_accounts", "Platform.Users.View", 1),
            ("Roles", "/platform/roles", "security", "Platform.Roles.View", 2),
            ("Branches", "/platform/branches", "account_tree", "Platform.Branches.Manage", 3),
            ("Departments", "/platform/departments", "corporate_fare", "Platform.Departments.Manage", 4),
            ("Allowance Rules", "/driver-allowance-rules", "rule", "Platform.Roles.Manage", 5),
        };

        foreach (var (name, route, icon, permission, sort) in adminMenus)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE pm SET pm.Route = @Route, pm.Icon = @Icon, pm.PermissionCode = @Permission, pm.SortOrder = @Sort
                FROM PlatformMenus pm
                INNER JOIN PlatformModules m ON m.Id = pm.ModuleId
                WHERE m.ModuleKey = 'administration' AND pm.Name = @Name;

                IF @@ROWCOUNT = 0
                INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive)
                SELECT m.Id, NULL, @Name, @Route, @Icon, @Permission, @Sort, 1
                FROM PlatformModules m WHERE m.ModuleKey = 'administration' AND NOT EXISTS (
                    SELECT 1 FROM PlatformMenus x WHERE x.ModuleId = m.Id AND x.Name = @Name
                );
                """, new { Name = name, Route = route, Icon = icon, Permission = permission, Sort = sort },
                cancellationToken: ct));
        }
    }

    private static async Task DeduplicatePlatformMenusAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE pm
            FROM PlatformMenus pm
            INNER JOIN (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY ModuleId, Name ORDER BY Id) AS rn
                FROM PlatformMenus
            ) ranked ON ranked.Id = pm.Id
            WHERE ranked.rn > 1;
            """, cancellationToken: ct));
    }

    private static async Task EnsurePlatformMenuUniqueIndexAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await CreateUniqueIndexIfMissingAsync(
            connection, "UX_PlatformMenus_ModuleId_Name", "PlatformMenus", "ModuleId, Name", ct);
    }

    private static async Task EnsureDefaultSuperAdminAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO UserRoles (UserId, RoleId)
            SELECT u.Id, r.Id
            FROM Users u
            INNER JOIN Roles r ON r.TenantId = u.TenantId AND r.Code = 'SUPER_ADMIN'
            WHERE u.TenantId = 1 AND u.Role = 1 AND u.IsDeleted = 0
              AND NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);
            """, cancellationToken: ct));
    }

    private static async Task SeedUserRolesFromLegacyAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO UserRoles (UserId, RoleId)
            SELECT u.Id, r.Id
            FROM Users u
            INNER JOIN Roles r ON r.TenantId = u.TenantId
            WHERE u.IsDeleted = 0
              AND r.Code = CASE u.Role
                WHEN 1 THEN 'TENANT_ADMIN'
                WHEN 2 THEN 'DISPATCHER'
                WHEN 3 THEN 'DRIVER'
                WHEN 4 THEN 'ACCOUNTANT'
                ELSE 'TENANT_ADMIN'
              END
              AND NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);
            """, cancellationToken: ct));
    }

    private static async Task SeedPlatformModulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var modules = new (string Name, string Key, string Icon, int Sort, bool Collapsible)[]
        {
            ("Dashboard", "dashboard", "dashboard", 1, false),
            ("Operations", "operations", "settings", 2, true),
            ("Fleet Management", "fleet", "local_shipping", 3, true),
            ("Customers", "customers", "groups", 4, true),
            ("Finance", "finance", "account_balance_wallet", 5, true),
            ("Analytics", "analytics", "bar_chart", 6, true),
            ("Administration", "administration", "admin_panel_settings", 7, true),
        };

        foreach (var (name, key, icon, sort, collapsible) in modules)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = @Key)
                INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
                VALUES (@Name, @Key, @Icon, @Sort, @Collapsible);
                """, new { Name = name, Key = key, Icon = icon, Sort = sort, Collapsible = collapsible }, cancellationToken: ct));
        }
    }

    private static async Task SeedPlatformMenusAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM PlatformMenus", cancellationToken: ct));
        if (count > 0) return;

        await SeedMenuItemAsync(connection, "dashboard", null, "Dashboard", "/dashboard", "dashboard", "Platform.Dashboard.View", 1, ct);
        await SeedMenuItemAsync(connection, "operations", null, "Bookings", "/bookings", "confirmation_number", "Booking.View", 1, ct);
        await SeedMenuItemAsync(connection, "operations", null, "Trips", "/bookings", "route", "Trip.View", 2, ct);
        await SeedMenuItemAsync(connection, "operations", null, "Routes", "/routes", "alt_route", "Route.View", 3, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "Vehicles", "/vehicles", "directions_bus", "Vehicle.View", 1, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "Drivers", "/drivers", "badge", "Driver.View", 2, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "Assignments", "/fleet/assignments", "event_available", "Vehicle.View", 3, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "GPS Tracking", "/gps-tracking", "my_location", "GPS.View", 4, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "Maintenance", "/maintenance", "build", "Maintenance.View", 5, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "Inspection", "/fleet/inspections", "fact_check", "Vehicle.View", 6, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "Compliance", "/fleet/compliance", "verified_user", "Vehicle.View", 7, ct);
        await SeedMenuItemAsync(connection, "fleet", null, "Fuel", "/fuel-logs", "local_gas_station", "Fuel.View", 8, ct);
        await SeedMenuItemAsync(connection, "customers", null, "Customers", "/customers", "group", "Customer.View", 1, ct);
        await SeedMenuItemAsync(connection, "finance", null, "Payments", "/payments", "payments", "Payment.View", 1, ct);
        await SeedMenuItemAsync(connection, "analytics", null, "Reports", "/reports", "insights", "Report.View", 1, ct);
        await SeedMenuItemAsync(connection, "analytics", null, "Audit Logs", "/audit-logs", "history", "Platform.AuditLogs.View", 2, ct);
        await SeedMenuItemAsync(connection, "administration", null, "Users", "/users", "manage_accounts", "Platform.Users.View", 1, ct);
        await SeedMenuItemAsync(connection, "administration", null, "Roles", "/users", "security", "Platform.Roles.View", 2, ct);
        await SeedMenuItemAsync(connection, "administration", null, "Allowance Rules", "/driver-allowance-rules", "rule", "Platform.Roles.Manage", 3, ct);
    }

    private static async Task SeedMenuItemAsync(
        System.Data.IDbConnection connection,
        string moduleKey,
        int? parentId,
        string name,
        string route,
        string icon,
        string? permissionCode,
        int sortOrder,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive)
            SELECT m.Id, @ParentId, @Name, @Route, @Icon, @PermissionCode, @SortOrder, 1
            FROM PlatformModules m WHERE m.ModuleKey = @ModuleKey;
            """, new { ModuleKey = moduleKey, ParentId = parentId, Name = name, Route = route, Icon = icon, PermissionCode = permissionCode, SortOrder = sortOrder }, cancellationToken: ct));
    }

    private static async Task SeedDefaultBranchAndDepartmentsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM Branches WHERE TenantId = 1)
            INSERT INTO Branches (TenantId, BranchCode, Name, Address, City, Country, TimeZone, CurrencyCode, Status, IsGpsEnabled, IsActive, CreatedAt)
            VALUES (1, N'HQ-001', N'Head Office', N'Main branch', N'Karachi', N'Pakistan', N'Asia/Karachi', N'PKR', 1, 1, 1, GETUTCDATE());

            IF NOT EXISTS (SELECT 1 FROM Departments WHERE TenantId = 1)
            BEGIN
                INSERT INTO Departments (TenantId, Name, IsActive, CreatedAt) VALUES
                (1, N'Operations', 1, GETUTCDATE()),
                (1, N'Finance', 1, GETUTCDATE()),
                (1, N'Fleet', 1, GETUTCDATE()),
                (1, N'HR', 1, GETUTCDATE());
            END
            """, cancellationToken: ct));
    }

    private static async Task CreateUniqueIndexIfMissingAsync(
        System.Data.IDbConnection connection,
        string indexName,
        string tableName,
        string columnList,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = @IndexName AND object_id = OBJECT_ID(@TableName)
            )
            EXEC('CREATE UNIQUE INDEX [' + @IndexName + '] ON [' + @TableName + '] (' + @ColumnList + ')');
            """, new { IndexName = indexName, TableName = tableName, ColumnList = columnList },
            cancellationToken: ct));
    }

    private static async Task CreateIndexIfMissingAsync(
        System.Data.IDbConnection connection,
        string indexName,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = @IndexName AND object_id = OBJECT_ID(@TableName)
            )
            EXEC('CREATE INDEX [' + @IndexName + '] ON [' + @TableName + '] ([' + @ColumnName + '])');
            """, new { IndexName = indexName, TableName = tableName, ColumnName = columnName },
            cancellationToken: ct));
    }

    private static async Task AddColumnIfMissingAsync(
        System.Data.IDbConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken ct)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column",
            new { Table = table, Column = column },
            cancellationToken: ct));

        if (exists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"ALTER TABLE [{table}] ADD [{column}] {definition}",
                cancellationToken: ct));
        }
    }
}
