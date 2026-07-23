namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stage 5 Feature Registry seed (source for migration + API fallback).
/// Features are functional capabilities within a Module — not runtime feature flags.
/// </summary>
public static class FeatureRegistrySeed
{
    public sealed record Entry(
        string FeatureKey,
        string ModuleKey,
        string Name,
        string DisplayName,
        string Description,
        string Category,
        string Icon,
        string? Route,
        int SortOrder,
        bool Visible,
        string Status,
        bool IsMobileSupported,
        bool IsAISupported,
        bool IsGPSSupported,
        string? DocumentationUrl);

    public static IReadOnlyList<Entry> All { get; } =
    [
        E("dashboard", "DASHBOARD", "Dashboard", "Dashboard", "Operational home dashboards",
            "Administration", "dashboard", "/dashboard", 10, true, "Active", true, false, false),
        E("vehicles", "FLEET", "Vehicles", "Vehicles", "Vehicle registry and fleet assets",
            "Vehicles", "directions_car", "/vehicles", 20, true, "Active", true, false, true),
        E("drivers", "FLEET", "Drivers", "Drivers", "Driver profiles and assignments",
            "Drivers", "badge", "/drivers", 21, true, "Active", true, false, false),
        E("fuel-logs", "FLEET", "Fuel Logs", "Fuel", "Fuel receipt and consumption tracking",
            "Fuel", "local_gas_station", "/fuel-logs", 22, true, "Active", true, false, false),
        E("maintenance", "FLEET", "Maintenance", "Maintenance", "Service and maintenance workflows",
            "Maintenance", "build", "/maintenance", 23, true, "Active", true, false, false),
        E("gps-tracking", "GPS", "GPS Tracking", "GPS", "Live tracking and GPS telemetry",
            "GPS", "my_location", "/gps-tracking", 30, true, "Active", true, false, true),
        E("rental", "RENTAL", "Vehicle Rental", "Rental", "Rental bookings and fleet hire",
            "Fleet", "car_rental", "/rental", 40, true, "Active", false, false, false),
        E("bookings", "TRAVEL", "Bookings", "Bookings", "Travel agency bookings",
            "Bookings", "event_note", "/bookings", 50, true, "Active", true, false, false),
        E("routes", "TRAVEL", "Routes", "Routes", "Route planning and catalog",
            "Travel", "alt_route", "/routes", 51, true, "Active", true, false, false),
        E("trips", "TRAVEL", "Trips", "Trips", "Trip lifecycle and dispatch",
            "Trips", "route", "/trips", 52, true, "Active", true, false, false),
        E("customers", "CRM", "Customers", "Customers", "CRM customer directory",
            "CRM", "groups", "/customers", 60, true, "Active", false, false, false),
        E("payments", "FINANCE", "Payments", "Payments", "Payments and collections",
            "Finance", "payments", "/payments", 70, true, "Active", false, false, false),
        E("hr", "HR", "HR", "HR", "Human resources module features",
            "Administration", "badge", "/hr", 80, true, "Active", false, false, false),
        E("reports", "ANALYTICS", "Reports", "Reports", "Analytics and operational reports",
            "Reports", "bar_chart", "/reports", 90, true, "Active", true, true, false),
        E("audit-logs", "ANALYTICS", "Audit Logs", "Audit Logs", "Platform audit trail viewing",
            "Administration", "history", "/audit-logs", 91, true, "Active", false, false, false),
        E("users", "ACCESS", "Users", "Users", "User directory within access control",
            "Administration", "people", "/users", 100, true, "Active", false, false, false),
        E("driver-allowance-rules", "ACCESS", "Driver Allowance Rules", "Allowance Rules",
            "Allowance rule configuration", "Administration", "rule", "/platform/access-control", 101,
            true, "Active", false, false, false),
        // Catalog-only / future capability metadata (not toggleable until Active)
        E("booking-import", "TRAVEL", "Booking Import", "Booking Import", "Import bookings from external sources",
            "Bookings", "upload_file", null, 53, true, "ComingSoon", false, false, false),
        E("ai-copilot", "AI", "AI Copilot", "AI Copilot", "In-app AI assistant for operations",
            "AI", "smart_toy", "/ai", 120, true, "Beta", true, true, false),
    ];

    private static Entry E(
        string featureKey,
        string moduleKey,
        string name,
        string displayName,
        string description,
        string category,
        string icon,
        string? route,
        int sortOrder,
        bool visible,
        string status,
        bool mobile,
        bool ai,
        bool gps)
        => new(featureKey, moduleKey, name, displayName, description, category, icon, route,
            sortOrder, visible, status, mobile, ai, gps, null);

    public static bool IsToggleable(string status)
        => string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "Beta", StringComparison.OrdinalIgnoreCase);

    public static Entry? Find(string featureKey)
        => All.FirstOrDefault(e =>
            string.Equals(e.FeatureKey, featureKey, StringComparison.OrdinalIgnoreCase));
}
