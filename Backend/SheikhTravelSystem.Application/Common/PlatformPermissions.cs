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
    public const string BranchesManage = "Platform.Branches.Manage";
    public const string DepartmentsManage = "Platform.Departments.Manage";
    public const string AuditLogsView = "Platform.AuditLogs.View";
    public const string MenusManage = "Platform.Menus.Manage";
    public const string SettingsView = "Platform.Settings.View";
    public const string SettingsManage = "Platform.Settings.Manage";

    public static readonly string[] All =
    [
        DashboardView, UsersView, UsersCreate, UsersEdit,
        RolesView, RolesManage, TenantsView, TenantsManage,
        BranchesManage, DepartmentsManage, AuditLogsView, MenusManage,
        SettingsView, SettingsManage
    ];
}

public static class PlatformRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string TenantAdmin = "TENANT_ADMIN";
}
