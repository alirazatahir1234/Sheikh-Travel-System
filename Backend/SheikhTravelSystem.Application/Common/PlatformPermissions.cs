namespace SheikhTravelSystem.Application.Common;

public static class PlatformPermissions
{
    public const string DashboardView = "Platform.Dashboard.View";
    public const string UsersView = "Platform.Users.View";
    public const string UsersCreate = "Platform.Users.Create";
    public const string UsersEdit = "Platform.Users.Edit";
    public const string RolesView = "Platform.Roles.View";
    public const string RolesManage = "Platform.Roles.Manage";
    public const string TenantsView = "Platform.Tenants.View";
    public const string TenantsManage = "Platform.Tenants.Manage";
    /// <summary>Cross-tenant act-as / admin vehicles / GPS platform reads (SG-05).</summary>
    public const string TenantsDataRead = "Platform.Tenants.DataRead";
    /// <summary>Cross-tenant audit trail (SG-07).</summary>
    public const string TenantsAuditReadAll = "Platform.Tenants.AuditReadAll";
    /// <summary>Cross-tenant user directory (SG-42).</summary>
    public const string TenantsUsersReadAll = "Platform.Tenants.UsersReadAll";
    /// <summary>Impersonation (seeded for SUPER_ADMIN; no endpoint yet).</summary>
    public const string TenantsImpersonate = "Platform.Tenants.Impersonate";
    public const string BranchesView = "Platform.Branches.View";
    public const string BranchesManage = "Platform.Branches.Manage";
    public const string DepartmentsManage = "Platform.Departments.Manage";
    public const string AuditLogsView = "Platform.AuditLogs.View";
    public const string AuditView = "Platform.Audit.View";
    public const string AuditManage = "Platform.Audit.Manage";
    public const string MenusManage = "Platform.Menus.Manage";
    public const string WorkspacesManage = "Platform.Workspaces.Manage";
    public const string DashboardsView = "Platform.Dashboards.View";
    public const string DashboardsManage = "Platform.Dashboards.Manage";
    public const string SecurityView = "Platform.Security.View";
    public const string SecurityManage = "Platform.Security.Manage";
    public const string SettingsView = "Platform.Settings.View";
    public const string SettingsManage = "Platform.Settings.Manage";
    public const string MigrationsView = "Platform.Migrations.View";
    public const string MigrationsManage = "Platform.Migrations.Manage";
    public const string SystemReset = "Platform.System.Reset";

    // Stage 16 — GPS Device Control Center
    public const string GpsControlView = "Platform.Gps.Control.View";
    public const string GpsManufacturersManage = "Platform.Gps.Manufacturers.Manage";
    public const string GpsModelsManage = "Platform.Gps.Models.Manage";
    public const string GpsCommandsManage = "Platform.Gps.Commands.Manage";
    public const string GpsTemplatesManage = "Platform.Gps.Templates.Manage";
    public const string GpsGatewaysManage = "Platform.Gps.Gateways.Manage";
    public const string GpsExecute = "Platform.Gps.Execute";
    public const string GpsBulkExecute = "Platform.Gps.BulkExecute";
    public const string GpsApprove = "Platform.Gps.Approve";
    public const string GpsHistoryView = "Platform.Gps.History.View";
    public const string GpsSimulatorUse = "Platform.Gps.Simulator.Use";

    public static readonly string[] All =
    [
        DashboardView, UsersView, UsersCreate, UsersEdit,
        RolesView, RolesManage, TenantsView, TenantsManage,
        TenantsDataRead, TenantsAuditReadAll, TenantsUsersReadAll, TenantsImpersonate,
        BranchesView, BranchesManage, DepartmentsManage, AuditLogsView, AuditView, AuditManage, MenusManage, WorkspacesManage,
        DashboardsView, DashboardsManage, SecurityView, SecurityManage,
        SettingsView, SettingsManage, MigrationsView, MigrationsManage, SystemReset,
        GpsControlView, GpsManufacturersManage, GpsModelsManage, GpsCommandsManage, GpsTemplatesManage,
        GpsGatewaysManage, GpsExecute, GpsBulkExecute, GpsApprove, GpsHistoryView, GpsSimulatorUse
    ];

    /// <summary>
    /// Permissions that may only succeed when the JWT tenant_id is the platform tenant.
    /// Not a role-name check — a tenant + claim check (SG-05 A2).
    /// </summary>
    public static bool IsPlatformScopePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;
        return permission.StartsWith("Gps.Command", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.Tenants.", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.Vehicles.", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.Gps.", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.Migrations.", StringComparison.OrdinalIgnoreCase)
            || permission.StartsWith("Platform.System.", StringComparison.OrdinalIgnoreCase);
    }
}

public static class PlatformRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string TenantAdmin = "TENANT_ADMIN";
    public const string PlatformAuditor = "PLATFORM_AUDITOR";
}
