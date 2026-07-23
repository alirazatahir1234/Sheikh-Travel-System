namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stage 9 Menu Registry seed metadata for PlatformMenus / PlatformModules.
/// Matched by Route (preferred) or Name during migration backfill.
/// </summary>
public static class MenuRegistrySeed
{
    public sealed record ModuleEntry(
        string ModuleKey,
        string DisplayName,
        string? Description,
        bool Visible = true);

    public sealed record MenuEntry(
        string Route,
        string Name,
        string DisplayName,
        string? Description,
        string Category,
        string? FeatureKey,
        string? ModuleKey,
        bool IsMobileSupported,
        bool Visible = true);

    public static IReadOnlyList<ModuleEntry> Modules { get; } =
    [
        new("dashboard", "Dashboard", "Home and operational dashboards"),
        new("operations", "Operations", "Bookings, routes, and trips"),
        new("fleet", "Fleet", "Vehicles, drivers, fuel, maintenance, GPS"),
        new("customers", "Customers", "CRM and customer management"),
        new("finance", "Finance", "Payments and financial ops"),
        new("analytics", "Analytics", "Reports and audit"),
        new("administration", "Administration", "Users and allowance rules"),
        new("organization", "Organization", "Companies, branches, hierarchy"),
        new("access_control", "Access Control", "Roles, permissions, policies"),
        new("platform", "Platform", "Platform administration hub"),
    ];

    public static IReadOnlyList<MenuEntry> Menus { get; } =
    [
        M("/dashboard", "Dashboard", "Dashboard", "Operational home", "Platform", null, "DASHBOARD", true),
        M("/bookings", "Bookings", "Bookings", "Travel bookings", "Travel", "bookings", "TRAVEL", true),
        M("/routes", "Routes", "Routes", "Route catalog", "Travel", null, "TRAVEL", false),
        M("/trips", "Trips", "Trips", "Trip lifecycle", "Travel", "trips", "TRAVEL", true),
        M("/vehicles", "Vehicles", "Vehicles", "Vehicle registry", "Fleet", "vehicles", "FLEET", true),
        M("/drivers", "Drivers", "Drivers", "Driver directory", "Fleet", "drivers", "FLEET", true),
        M("/gps-tracking", "GPS Tracking", "GPS Tracking", "Live tracking", "Fleet", "gps-tracking", "GPS", true),
        M("/fuel-logs", "Fuel Logs", "Fuel", "Fuel logs and receipts", "Fleet", "fuel-logs", "FLEET", false),
        M("/maintenance", "Maintenance", "Maintenance", "Service workflows", "Fleet", "maintenance", "FLEET", true),
        M("/customers", "Customers", "Customers", "Customer CRM", "CRM", null, "CRM", false),
        M("/payments", "Payments", "Payments", "Collections", "Finance", null, "FINANCE", false),
        M("/reports", "Reports", "Reports", "Analytics and reports", "Analytics", null, "ANALYTICS", true),
        M("/audit-logs", "Audit Logs", "Audit Logs", "Security audit trail", "Analytics", null, "ANALYTICS", false),
        M("/users", "Users", "Users", "User management", "Platform", null, "ACCESS", false),
        M("/platform/tenants", "Companies", "Companies", "Company registry", "Platform", null, "ACCESS", false),
        M("/platform/tenants", "Tenants", "Companies", "Company registry", "Platform", null, "ACCESS", false),
        M("/platform/organization-designer", "Hierarchy", "Hierarchy", "Org hierarchy designer", "Platform", null, "ACCESS", false),
        M("/platform/branches", "Branches", "Branches", "Branch management", "Platform", null, "ACCESS", false),
        M("/platform/departments", "Departments", "Departments", "Department management", "Platform", null, "ACCESS", false),
        M("/platform/access-control", "Access Control", "Access Control", "Users, roles, policies", "Platform", null, "ACCESS", false),
        M("/platform/access-control?tab=roles", "Roles", "Roles", "Role management", "Platform", null, "ACCESS", false),
        M("/platform/module-management", "Modules", "Modules", "Module registry", "Platform", null, null, false),
        M("/platform/feature-management", "Features", "Features", "Feature registry", "Platform", null, null, false),
        M("/platform/subscription-management", "Plans", "Plans", "Subscription plans", "Platform", null, null, false),
        M("/platform/migrations", "Migration Manager", "Migrations", "Schema migrations", "Platform", null, null, false),
        M("/platform/maintenance", "Database Reset", "Database Reset", "Dev/staging reset", "Platform", null, null, false),
        M("/platform/menu-management", "Menus", "Menus", "Navigation catalog", "Platform", null, null, false),
    ];

    private static MenuEntry M(
        string route, string name, string displayName, string? description,
        string category, string? featureKey, string? moduleKey, bool mobile)
        => new(route, name, displayName, description, category, featureKey, moduleKey, mobile);

    public static MenuEntry? FindByRouteOrName(string? route, string name)
    {
        if (!string.IsNullOrWhiteSpace(route))
        {
            var byRoute = Menus.FirstOrDefault(m =>
                string.Equals(m.Route, route, StringComparison.OrdinalIgnoreCase));
            if (byRoute is not null) return byRoute;
        }

        return Menus.FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static ModuleEntry? FindModule(string moduleKey)
        => Modules.FirstOrDefault(m =>
            string.Equals(m.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase));
}
