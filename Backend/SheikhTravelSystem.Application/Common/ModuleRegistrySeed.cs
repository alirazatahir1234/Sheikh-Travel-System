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
        E("GPS", "Fleet Tracking", "Fleet Tracking", "Live tracking and GPS telemetry", "Fleet", "my_location", "/gps-tracking", 30,
            ["FLEET"], true, true, false, true, "Active", ["gps-tracking"]),
        // Major Alerts module (parent) + per-type children — Category "Alerts"
        E(AlertModuleCatalog.AlertsModuleCode, "Fleet Alerts", "Fleet Alerts",
            "Fleet alert types — enable the parent and toggle individual alert capabilities per company",
            AlertModuleCatalog.AlertsCategory, "notifications_active", "/gps-tracking/alerts", 200,
            ["GPS"], true, true, false, true, "Active", ["alerts"]),
        // Module-governed alert children only — geofence/speed types are GpsAddOnFeatureCatalog TenantFeatures.
        ..AlertModuleCatalog.ModuleGovernedTypes.Select(t => E(
            t.ModuleCode, t.Name, t.DisplayName, t.Description,
            AlertModuleCatalog.AlertsCategory, t.Icon, "/gps-tracking/alerts", t.SortOrder,
            [AlertModuleCatalog.AlertsModuleCode], true, true, false, true, "Active", [t.LegacyKey])),
        E("TRAVEL", "Routes", "Routes", "Roads and route catalog (independent of bookings/trips)", "Travel", "alt_route", "/routes", 50,
            ["DASHBOARD"], true, true, false, false, "Active", ["routes"]),
        // Opt-in: bookings + trips (not in DefaultModuleCodes — avoid over-provisioning)
        E("BOOKINGS", "Bookings & Trips", "Bookings & Trips", "Booking and trip workflows — enable per company", "Travel", "event_note", "/bookings", 51,
            ["DASHBOARD"], true, true, false, false, "Active", ["bookings", "trips"]),
        E("CRM", "CRM", "Customers", "Customer relationship management", "CRM", "groups", "/customers", 60,
            [], true, false, false, false, "Active", ["customers"]),
        E("FINANCE", "Finance", "Finance", "Payments and financial operations", "Finance", "payments", "/payments", 70,
            [], true, false, false, false, "Active", ["payments"]),
        E("ANALYTICS", "Analytics Pro", "Analytics Pro", "Analytics, reports, and audit logs", "Analytics", "bar_chart", "/reports", 90,
            ["DASHBOARD"], true, true, true, false, "Active", ["reports", "audit-logs"]),
        E("ACCESS", "Access Control", "Access", "Users, roles, and allowance rules", "Platform", "admin_panel_settings", "/platform/access-control", 100,
            ["DASHBOARD"], true, false, false, false, "Active", ["users", "driver-allowance-rules"]),

        // Active catalog-only (shipped under parent TenantModuleCatalog toggles — not separately enableable)
        E("PLATFORM", "Platform", "Platform", "Platform administration shell", "Platform", "settings_applications", "/platform", 5,
            [], true, false, false, false, "Active", []),
        E("TRIPS", "Trips", "Trips", "Trip lifecycle and dispatch (included in Bookings & Trips)", "Travel", "route", "/trips", 52,
            ["BOOKINGS"], true, true, false, false, "Active", []),
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

        // Premium subscription add-ons — default off; not in plan defaults.
        // Alert premium children (ROAD_SPEED_ALERTS, ALERT_OVERSPEED, ALERT_GPS_LOST, ALERT_LOW_FUEL)
        // come from AlertModuleCatalog.Types (IsPremium / DefaultEnabled).
        E("AI", "SheikhGo AI Assistant", "SheikhGo AI Assistant", "Premium SheikhGo AI Assistant for operations chat and tools", "Platform", "smart_toy", "/ai", 120,
            ["DASHBOARD"], true, true, true, false, "Active", ["ai"]),

        // Coming soon / roadmap catalog-only product modules
        E("RENTAL", "Vehicle Rental", "Rental", "Vehicle hire and rental workflows", "Fleet", "car_rental", "/rental", 40,
            ["FLEET"], true, false, false, false, "Active", ["rental"]),
        E("HR", "HR", "HR", "Human resources module", "HR", "badge", null, 80,
            [], true, false, false, false, "ComingSoon", ["hr"]),
        E("INVOICES", "Invoices", "Invoices", "Invoicing and billing documents", "Finance", "receipt_long", "/invoices", 72,
            ["FINANCE"], true, false, false, false, "Active", []),
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

    /// <summary>
    /// Whether a registry entry can be toggled for a tenant
    /// (Active + in <see cref="TenantModuleCatalog"/> + not a core always-on module).
    /// </summary>
    public static bool IsToggleable(Entry entry)
        => IsEnableable(entry.Code)
           && string.Equals(entry.Status, "Active", StringComparison.OrdinalIgnoreCase)
           && !TenantModuleCatalog.IsCore(entry.Code);

    /// <summary>
    /// Expands requested enableable codes with transitive Active-catalog dependencies
    /// (e.g. GPS → FLEET → DASHBOARD). Non-toggleable codes are ignored.
    /// </summary>
    public static IReadOnlyList<string> ExpandToggleableWithDependencies(IEnumerable<string> requestedCodes)
    {
        var byCode = All
            .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string code)
        {
            if (!byCode.TryGetValue(code, out var entry))
                return;
            // Core always-on modules (GPS) are included even though they are not toggleable.
            var include = IsToggleable(entry) || TenantModuleCatalog.IsCore(entry.Code);
            if (!include || !result.Add(entry.Code))
                return;
            foreach (var dep in entry.Dependencies)
                Add(dep);
        }

        foreach (var code in requestedCodes ?? [])
        {
            if (!string.IsNullOrWhiteSpace(code))
                Add(code.Trim());
        }

        return result.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
