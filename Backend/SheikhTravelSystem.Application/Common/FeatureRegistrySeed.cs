namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stage 5 Feature Registry seed (source for migration + API fallback).
/// <para>
/// Module legacy keys (vehicles, gps-tracking, …) remain as catalog metadata for menus /
/// back-compat but are <b>not</b> TenantFeatures toggles — those capabilities are governed by
/// <see cref="TenantModuleCatalog"/> / TenantModules.
/// </para>
/// <para>
/// True opt-in add-ons (e.g. <see cref="GpsAddOnFeatureCatalog"/>) are Active and toggleable
/// under their parent module.
/// </para>
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

    /// <summary>Status for module-legacy capability rows — visible catalog, not TenantFeatures toggles.</summary>
    public const string ModuleLegacyStatus = "ModuleLegacy";

    public static IReadOnlyList<Entry> All { get; } =
    [
        // Module-legacy capability metadata (not TenantFeatures toggles).
        E("dashboard", "DASHBOARD", "Dashboard", "Dashboard", "Operational home dashboards",
            "Administration", "dashboard", "/dashboard", 10, true, ModuleLegacyStatus, true, false, false),
        E("vehicles", "FLEET", "Vehicles", "Vehicles", "Vehicle registry and fleet assets",
            "Vehicles", "directions_car", "/vehicles", 20, true, ModuleLegacyStatus, true, false, true),
        E("drivers", "FLEET", "Drivers", "Drivers", "Driver profiles and assignments",
            "Drivers", "badge", "/drivers", 21, true, ModuleLegacyStatus, true, false, false),
        E("fuel-logs", "FLEET", "Fuel Logs", "Fuel", "Fuel receipt and consumption tracking",
            "Fuel", "local_gas_station", "/fuel-logs", 22, true, ModuleLegacyStatus, true, false, false),
        E("maintenance", "FLEET", "Maintenance", "Maintenance", "Service and maintenance workflows",
            "Maintenance", "build", "/maintenance", 23, true, ModuleLegacyStatus, true, false, false),
        E("gps-tracking", "GPS", "GPS Tracking", "GPS", "Core live tracking (TenantModules GPS)",
            "GPS", "my_location", "/gps-tracking", 30, true, ModuleLegacyStatus, true, false, true),
        E("rental", "RENTAL", "Vehicle Rental", "Rental", "Rental bookings and fleet hire",
            "Fleet", "car_rental", "/rental", 40, true, ModuleLegacyStatus, false, false, false),
        E("bookings", "TRAVEL", "Bookings", "Bookings", "Travel agency bookings",
            "Bookings", "event_note", "/bookings", 50, true, ModuleLegacyStatus, true, false, false),
        E("routes", "TRAVEL", "Routes", "Routes", "Route planning and catalog",
            "Travel", "alt_route", "/routes", 51, true, ModuleLegacyStatus, true, false, false),
        E("trips", "TRAVEL", "Trips", "Trips", "Trip lifecycle and dispatch",
            "Trips", "route", "/trips", 52, true, ModuleLegacyStatus, true, false, false),
        E(TenantModuleCatalog.TravelAgencyLegacyKey, TenantModuleCatalog.TravelAgencyModuleCode,
            "TravelNexus", "TravelNexus", "Travel agency, visa and Umrah management",
            "Travel", "flight_takeoff", "/travel", 55, true, ModuleLegacyStatus, false, false, false),
        E("customers", "CRM", "Customers", "Customers", "CRM customer directory",
            "CRM", "groups", "/customers", 60, true, ModuleLegacyStatus, false, false, false),
        E("payments", "FINANCE", "Payments", "Payments", "Payments and collections",
            "Finance", "payments", "/payments", 70, true, ModuleLegacyStatus, false, false, false),
        E("hr", "HR", "HR", "HR", "Human resources module features",
            "Administration", "badge", "/hr", 80, true, ModuleLegacyStatus, false, false, false),
        E("reports", "ANALYTICS", "Reports", "Reports", "Analytics and operational reports",
            "Reports", "bar_chart", "/reports", 90, true, ModuleLegacyStatus, true, true, false),
        E("audit-logs", "ANALYTICS", "Audit Logs", "Audit Logs", "Platform audit trail viewing",
            "Administration", "history", "/audit-logs", 91, true, ModuleLegacyStatus, false, false, false),
        E("users", "ACCESS", "Users", "Users", "User directory within access control",
            "Administration", "people", "/users", 100, true, ModuleLegacyStatus, false, false, false),
        E("driver-allowance-rules", "ACCESS", "Driver Allowance Rules", "Allowance Rules",
            "Allowance rule configuration", "Administration", "rule", "/platform/access-control", 101,
            true, ModuleLegacyStatus, false, false, false),

        // GPS opt-in add-ons (TenantFeatures) — shown under Add-ons in admin UI.
        ..GpsAddOnFeatureCatalog.All.Select(a => E(
            a.FeatureKey, "GPS", a.DisplayName, a.DisplayName, a.Description,
            "GPS Add-ons", a.Icon,
            a.FeatureKey == GpsAddOnFeatureCatalog.RoutePlayback ? "/gps-tracking/history"
                : a.FeatureKey == GpsAddOnFeatureCatalog.Geofencing ? "/gps-tracking/geofences"
                : "/gps-tracking/alerts",
            a.SortOrder, true, "Active", true, false, true)),

        // Catalog-only / future capability metadata (not toggleable until Active)
        E("booking-import", "TRAVEL", "Booking Import", "Booking Import", "Import bookings from external sources",
            "Bookings", "upload_file", null, 53, true, "ComingSoon", false, false, false),
        // AI capability is TenantModules (AI) — not a TenantFeatures add-on toggle.
        E("ai-copilot", "AI", "SheikhGo AI Assistant", "SheikhGo AI Assistant", "Premium in-app SheikhGo AI Assistant for operations",
            "AI", "smart_toy", "/ai", 120, true, ModuleLegacyStatus, true, true, false),
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

    /// <summary>
    /// Features that may be written to TenantFeatures from admin UI
    /// (excludes module-legacy catalog rows).
    /// </summary>
    public static bool IsTenantFeatureToggle(Entry entry)
        => entry.Visible
           && IsToggleable(entry.Status)
           && !string.Equals(entry.Status, ModuleLegacyStatus, StringComparison.OrdinalIgnoreCase)
           && !TenantModuleCatalog.IsModuleLegacyFeatureKey(entry.FeatureKey);

    public static Entry? Find(string featureKey)
        => All.FirstOrDefault(e =>
            string.Equals(e.FeatureKey, featureKey, StringComparison.OrdinalIgnoreCase));
}
