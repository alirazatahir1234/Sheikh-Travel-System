namespace SheikhTravelSystem.Application.Common;

/// <summary>Stage 11 Dashboard Builder seed: definitions, widgets, and default layouts.</summary>
public static class DashboardRegistrySeed
{
    public sealed record DashboardSeed(
        string DashboardKey,
        string DisplayName,
        string? Description,
        string Audience,
        string? DefaultWorkspaceKey,
        string Category,
        int SortOrder,
        bool Visible,
        bool IsSystem);

    public sealed record WidgetSeed(
        string WidgetKey,
        string DisplayName,
        string Category,
        string? Icon,
        string? PermissionCode,
        string? FeatureKey,
        string? ModuleKey,
        bool SupportsErp,
        bool SupportsMobile,
        int SortOrder,
        bool Visible);

    public sealed record LayoutSeed(string DashboardKey, string WidgetKey, int SortOrder);

    public static IReadOnlyList<DashboardSeed> Dashboards { get; } =
    [
        new("erp.default", "ERP Default", "Default ERP operations dashboard", "ERP", "company", "ERP", 10, true, true),
        new("erp.fleet", "ERP Fleet", "Fleet-focused ERP dashboard", "ERP", "fleet", "ERP", 20, true, true),
        new("erp.trips", "ERP Trips", "Trips and dispatch ERP dashboard", "ERP", "trips", "ERP", 30, true, true),
        new("mobile.driver", "Mobile Driver", "Driver home layout", "Mobile", "driver", "Mobile", 40, true, true),
        new("mobile.fleet_ops", "Mobile Fleet Ops", "Fleet operations mobile home", "Mobile", "fleet", "Mobile", 50, true, true),
        new("mobile.admin", "Mobile Admin", "Admin / tenant admin mobile home", "Mobile", "company", "Mobile", 60, true, true),
    ];

    public static IReadOnlyList<WidgetSeed> Widgets { get; } =
    [
        new("opsHeader", "Ops header / greeting", "Shell", "waving_hand", null, null, null, true, true, 10, true),
        new("greeting", "Greeting", "Shell", "waving_hand", null, null, null, false, true, 11, true),
        new("platformBanner", "Platform banner", "Shell", "admin_panel_settings", "Platform.Dashboard.View", null, "platform", true, true, 12, true),
        new("universalSearchBar", "Universal search", "Shell", "search", null, null, null, true, true, 13, true),
        new("myVehicle", "My vehicle", "Driver", "directions_car", null, null, "fleet", false, true, 20, true),
        new("driverTripKpis", "Driver trip KPIs", "Driver", "route", null, null, "operations", false, true, 21, true),
        new("earnings", "Earnings", "Driver", "payments", null, null, "finance", false, true, 22, true),
        new("fleetHealthHeader", "Fleet health header", "Fleet", "health_and_safety", "Vehicle.View", null, "fleet", true, true, 30, true),
        new("fleetStatsStrip", "Fleet status strip", "Fleet", "speed", "GPS.View", null, "fleet", true, true, 31, true),
        new("fleetKpis", "Fleet KPIs", "Fleet", "local_shipping", "Vehicle.View", null, "fleet", true, true, 32, true),
        new("fleetStatusStrip", "Fleet GPS strip", "Fleet", "sensors", "GPS.View", null, "fleet", true, true, 33, true),
        new("opsKpiGrid", "Ops KPI grid", "Fleet", "grid_view", "Vehicle.View", null, "fleet", true, true, 34, true),
        new("liveFleetCard", "Live fleet card", "Fleet", "map", "GPS.View", null, "fleet", true, true, 35, true),
        new("liveMapPreview", "Live map preview", "Fleet", "map", "GPS.View", null, "fleet", true, true, 36, true),
        new("mapSummaryCard", "Map summary", "Fleet", "map", "GPS.View", null, "fleet", true, true, 37, true),
        new("attentionVehicles", "Attention vehicles", "Fleet", "warning", "Gps.AlertView", null, "fleet", true, true, 38, true),
        new("aiAttention", "AI attention", "AI", "auto_awesome", "Ai.View", null, null, true, true, 40, true),
        new("criticalAlertsList", "Critical alerts", "Alerts", "notification_important", "Gps.AlertView", null, "fleet", true, true, 50, true),
        new("recentAlerts", "Recent alerts", "Alerts", "notifications", "Gps.AlertView", null, "fleet", true, true, 51, true),
        new("tripKpis", "Trip KPIs", "Trips", "timeline", "Trip.View", null, "operations", true, true, 60, true),
        new("liveTripsPreview", "Live trips", "Trips", "local_taxi", "Trip.View", null, "operations", true, true, 61, true),
        new("pendingAssignments", "Pending assignments", "Trips", "assignment", "Trip.View", null, "operations", true, true, 62, true),
        new("todayOpsKpis", "Today ops KPIs", "Trips", "today", "Trip.View", null, "operations", true, true, 63, true),
        new("driverKpis", "Driver KPIs", "Drivers", "badge", "Driver.View", null, "fleet", true, true, 70, true),
        new("driverPerformance", "Driver performance", "Drivers", "insights", "Driver.ViewPerformance", null, "fleet", true, true, 71, true),
        new("complianceDocs", "Compliance docs", "Compliance", "folder", "Vehicle.View", null, "fleet", true, true, 72, true),
        new("maintenanceKpis", "Maintenance KPIs", "Maintenance", "build", "Maintenance.View", null, "fleet", true, true, 80, true),
        new("maintenanceCost", "Maintenance cost", "Maintenance", "handyman", "Maintenance.View", null, "fleet", true, true, 81, true),
        new("fuelSummary", "Fuel summary", "Fuel", "local_gas_station", "Fuel.View", null, "fleet", true, true, 82, true),
        new("fuelCost", "Fuel cost", "Fuel", "local_gas_station", "Fuel.View", null, "fleet", true, true, 83, true),
        new("financeKpis", "Finance KPIs", "Finance", "account_balance", "Payment.View", null, "finance", true, true, 90, true),
        new("recentActivities", "Recent activities", "Activity", "history", null, null, null, true, true, 100, true),
        new("quickActions", "Quick actions", "Shell", "bolt", null, null, null, true, true, 110, true),
        new("primaryKpis", "Primary KPIs", "Shell", "analytics", null, null, "dashboard", true, true, 15, true),
    ];

    public static IReadOnlyList<LayoutSeed> Layouts { get; } =
    [
        // mobile.driver
        new("mobile.driver", "opsHeader", 10),
        new("mobile.driver", "myVehicle", 20),
        new("mobile.driver", "driverTripKpis", 30),
        new("mobile.driver", "earnings", 40),
        new("mobile.driver", "quickActions", 50),

        // mobile.fleet_ops
        new("mobile.fleet_ops", "opsHeader", 10),
        new("mobile.fleet_ops", "universalSearchBar", 20),
        new("mobile.fleet_ops", "fleetHealthHeader", 30),
        new("mobile.fleet_ops", "fleetStatsStrip", 40),
        new("mobile.fleet_ops", "opsKpiGrid", 50),
        new("mobile.fleet_ops", "liveMapPreview", 60),
        new("mobile.fleet_ops", "aiAttention", 70),
        new("mobile.fleet_ops", "criticalAlertsList", 80),
        new("mobile.fleet_ops", "attentionVehicles", 90),
        new("mobile.fleet_ops", "quickActions", 100),

        // mobile.admin
        new("mobile.admin", "opsHeader", 10),
        new("mobile.admin", "platformBanner", 20),
        new("mobile.admin", "universalSearchBar", 30),
        new("mobile.admin", "fleetHealthHeader", 40),
        new("mobile.admin", "fleetStatsStrip", 50),
        new("mobile.admin", "opsKpiGrid", 60),
        new("mobile.admin", "mapSummaryCard", 70),
        new("mobile.admin", "aiAttention", 80),
        new("mobile.admin", "criticalAlertsList", 90),
        new("mobile.admin", "attentionVehicles", 100),
        new("mobile.admin", "quickActions", 110),

        // erp.default
        new("erp.default", "primaryKpis", 10),
        new("erp.default", "aiAttention", 20),
        new("erp.default", "criticalAlertsList", 30),
        new("erp.default", "recentActivities", 40),
        new("erp.default", "quickActions", 50),

        // erp.fleet
        new("erp.fleet", "fleetHealthHeader", 10),
        new("erp.fleet", "fleetKpis", 20),
        new("erp.fleet", "fleetStatusStrip", 30),
        new("erp.fleet", "liveFleetCard", 40),
        new("erp.fleet", "criticalAlertsList", 50),
        new("erp.fleet", "attentionVehicles", 60),
        new("erp.fleet", "quickActions", 70),

        // erp.trips
        new("erp.trips", "tripKpis", 10),
        new("erp.trips", "liveTripsPreview", 20),
        new("erp.trips", "pendingAssignments", 30),
        new("erp.trips", "todayOpsKpis", 40),
        new("erp.trips", "quickActions", 50),
    ];

    public static string RoleDefaultDashboard(string? roleCode, bool preferMobile = true) =>
        roleCode?.ToUpperInvariant() switch
        {
            "DRIVER" or "FIELD_DRIVER" => preferMobile ? "mobile.driver" : "erp.default",
            "FLEET_MANAGER" or "DRIVER_MANAGER" => preferMobile ? "mobile.fleet_ops" : "erp.fleet",
            "DISPATCHER" => preferMobile ? "mobile.fleet_ops" : "erp.trips",
            "SUPER_ADMIN" or "TENANT_ADMIN" or "ADMIN" => preferMobile ? "mobile.admin" : "erp.default",
            "ACCOUNTANT" => preferMobile ? "mobile.admin" : "erp.default",
            _ => preferMobile ? "mobile.fleet_ops" : "erp.default"
        };

    public static string AudienceFallback(bool preferMobile) =>
        preferMobile ? "mobile.driver" : "erp.default";
}
