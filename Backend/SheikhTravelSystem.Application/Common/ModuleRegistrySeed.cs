namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stage 3 Module Registry seed metadata (source for migration + API fallback).
/// Enableable Active codes remain aligned with <see cref="TenantModuleCatalog"/>.
/// </summary>
public static class ModuleRegistrySeed
{
    public sealed record Entry(
        string Code,
        string Name,
        string DisplayName,
        string Description,
        string Category,
        string Version,
        string Icon,
        string? Route,
        int SortOrder,
        string[] Dependencies,
        bool Visible,
        bool IsMobileSupported,
        bool IsAISupported,
        bool IsGPSSupported,
        string Status,
        string? DocumentationUrl,
        string[] LegacyKeys);

    public static IReadOnlyList<Entry> All { get; } =
    [
        // Active enableable (TenantModuleCatalog)
        E("DASHBOARD", "Dashboard", "Dashboard", "Operational home dashboards", "Platform", "dashboard", "/dashboard", 10,
            [], true, true, false, false, "Active", ["dashboard"]),
        E("FLEET", "Fleet Management", "Fleet", "Vehicles, drivers, fuel, and maintenance", "Fleet", "directions_car", "/vehicles", 20,
            ["DASHBOARD"], true, true, false, true, "Active", ["vehicles", "drivers", "fuel-logs", "maintenance"]),
        E("GPS", "Fleet Tracking", "GPS", "Live tracking and GPS telemetry", "Fleet", "my_location", "/gps-tracking", 30,
            ["FLEET"], true, true, false, true, "Active", ["gps-tracking"]),
        E("RENTAL", "Vehicle Rental", "Rental", "Vehicle hire and rental workflows", "Fleet", "car_rental", "/rental", 40,
            ["FLEET"], true, false, false, false, "Active", ["rental"]),
        E("TRAVEL", "Travel Agency", "Travel", "Bookings, routes, and trips", "Travel", "flight", "/bookings", 50,
            ["DASHBOARD"], true, true, false, false, "Active", ["bookings", "routes", "trips"]),
        E("CRM", "CRM", "Customers", "Customer relationship management", "CRM", "groups", "/customers", 60,
            [], true, false, false, false, "Active", ["customers"]),
        E("FINANCE", "Finance", "Finance", "Payments and financial operations", "Finance", "payments", "/payments", 70,
            [], true, false, false, false, "Active", ["payments"]),
        E("HR", "HR", "HR", "Human resources module", "HR", "badge", "/hr", 80,
            [], true, false, false, false, "Active", ["hr"]),
        E("ANALYTICS", "Analytics Pro", "Reports", "Analytics, reports, and audit logs", "Analytics", "bar_chart", "/reports", 90,
            ["DASHBOARD"], true, true, true, false, "Active", ["reports", "audit-logs"]),
        E("ACCESS", "Access Control", "Access", "Users, roles, and allowance rules", "Platform", "admin_panel_settings", "/platform/access-control", 100,
            ["DASHBOARD"], true, false, false, false, "Active", ["users", "driver-allowance-rules"]),

        // Active catalog-only (shipped under parent TenantModuleCatalog toggles — not separately enableable)
        E("PLATFORM", "Platform", "Platform", "Platform administration shell", "Platform", "settings_applications", "/platform", 5,
            [], true, false, false, false, "Active", []),
        E("BOOKINGS", "Bookings", "Bookings", "Travel booking workflows (included in Travel)", "Travel", "event_note", "/bookings", 51,
            ["TRAVEL"], true, true, false, false, "Active", []),
        E("TRIPS", "Trips", "Trips", "Trip lifecycle and dispatch (included in Travel)", "Travel", "route", "/trips", 52,
            ["TRAVEL"], true, true, false, false, "Active", []),
        E("DRIVERS", "Drivers", "Drivers", "Driver directory (included in Fleet)", "Fleet", "badge", "/drivers", 21,
            ["FLEET"], true, true, false, false, "Active", []),
        E("VEHICLES", "Vehicles", "Vehicles", "Vehicle registry (included in Fleet)", "Fleet", "local_shipping", "/vehicles", 22,
            ["FLEET"], true, true, false, true, "Active", []),
        E("MAINTENANCE", "Maintenance", "Maintenance", "Service workflows (included in Fleet)", "Fleet", "build", "/maintenance", 23,
            ["FLEET"], true, true, false, false, "Active", []),
        E("FUEL", "Fuel", "Fuel", "Fuel logs and receipts (included in Fleet)", "Fleet", "local_gas_station", "/fuel-logs", 24,
            ["FLEET"], true, true, false, false, "Active", []),
        E("PAYMENTS", "Payments", "Payments", "Collections (included in Finance)", "Finance", "attach_money", "/payments", 71,
            ["FINANCE"], true, false, false, false, "Active", []),
        E("SETTINGS", "Settings", "Settings", "Tenant and system settings", "Platform", "tune", "/settings", 160,
            [], true, false, false, false, "Active", []),

        // Beta
        E("NOTIFICATIONS", "Notifications", "Notifications", "Notification center and push", "Platform", "notifications", "/notifications", 110,
            [], true, true, true, false, "Beta", []),
        E("AI", "AI", "AI", "AI copilots and assistants", "Platform", "smart_toy", "/ai", 120,
            ["DASHBOARD"], true, true, true, false, "Beta", []),

        // Coming soon / roadmap catalog-only product modules
        E("INVOICES", "Invoices", "Invoices", "Invoicing and billing documents", "Finance", "receipt_long", null, 72,
            ["FINANCE"], true, false, false, false, "ComingSoon", []),
        E("SUPPORT", "Support", "Support", "Support desk and tickets", "Platform", "support_agent", null, 130,
            [], true, false, false, false, "ComingSoon", []),
        E("PAYROLL", "Payroll", "Payroll", "Payroll processing", "HR", "account_balance_wallet", null, 81,
            ["HR"], true, false, false, false, "ComingSoon", []),
        E("WAREHOUSE", "Warehouse", "Warehouse", "Warehouse operations", "Inventory", "warehouse", null, 140,
            [], true, false, false, false, "ComingSoon", []),
        E("INVENTORY", "Inventory", "Inventory", "Stock and inventory control", "Inventory", "inventory_2", null, 141,
            ["WAREHOUSE"], true, false, false, false, "ComingSoon", []),
        E("DOCUMENTS", "Documents", "Documents", "Document vault and compliance files", "Platform", "folder", null, 150,
            [], true, true, false, false, "ComingSoon", []),
    ];

    private static Entry E(
        string code,
        string name,
        string displayName,
        string description,
        string category,
        string icon,
        string? route,
        int sortOrder,
        string[] dependencies,
        bool visible,
        bool mobile,
        bool ai,
        bool gps,
        string status,
        string[] legacyKeys)
        => new(code, name, displayName, description, category, "1.0.0", icon, route, sortOrder,
            dependencies, visible, mobile, ai, gps, status, null, legacyKeys);

    public static bool IsEnableable(string code)
        => TenantModuleCatalog.All.Any(m =>
            string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase));
}
