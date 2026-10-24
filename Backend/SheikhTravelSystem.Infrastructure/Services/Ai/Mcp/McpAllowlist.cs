namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// ADR-AI-001: tenant SheikhGo AI Assistant may call reporting/read-only MCP groups only.
/// Architecture scaffolds, git, code search, and controlled writes are denied.
/// </summary>
public static class McpAllowlist
{
    /// <summary>Tools that accept (and require) a live tenantId argument.</summary>
    private static readonly HashSet<string> TenantScoped = new(StringComparer.OrdinalIgnoreCase)
    {
        // Bookings
        "list_bookings", "get_booking", "list_trips", "get_trip",
        "list_routes", "get_route", "summarize_booking_statuses",
        "summarize_trip_statuses", "get_booking_links",
        // Fleet
        "list_vehicles", "get_vehicle", "list_drivers", "get_driver",
        "list_vehicle_assignments", "get_vehicles_on_trips",
        "get_maintenance_due", "get_fleet_status", "list_fuel_records",
        // GPS / Traccar
        "get_devices", "get_live_positions", "get_vehicle_history",
        "get_geofences", "get_geofence_events", "get_gps_trips",
        // SmartOps
        "list_smart_alerts", "get_maintenance_reminders", "get_trip_delays",
        // Database — optional tenant hint for SELECT safety
        "run_readonly_query",
    };

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        // Database (live SELECT / schema — no Architecture file analyzers)
        "db_connection_status",
        "list_tables",
        "describe_table",
        "run_readonly_query",
        // Bookings
        "get_bookings_domain",
        "list_bookings",
        "get_booking",
        "list_trips",
        "get_trip",
        "list_routes",
        "get_route",
        "summarize_booking_statuses",
        "summarize_trip_statuses",
        "get_booking_links",
        // Fleet
        "get_fleet_domain",
        "list_vehicles",
        "get_vehicle",
        "list_drivers",
        "get_driver",
        "list_vehicle_assignments",
        "get_vehicles_on_trips",
        "get_maintenance_due",
        "get_fleet_status",
        "list_fuel_records",
        // GPS / Traccar (lat/lng only — no Maps keys)
        "get_gps_domain",
        "get_devices",
        "get_live_positions",
        "get_vehicle_history",
        "get_geofences",
        "get_geofence_events",
        "get_gps_trips",
        "get_traccar_status",
        // SmartOps
        "get_smartops_domain",
        "list_smart_alerts",
        "get_maintenance_reminders",
        "get_trip_delays",
    };

    /// <summary>Hard deny — Architecture / writes / code (never callable from tenant SheikhGo AI Assistant).</summary>
    private static readonly HashSet<string> Denied = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_repository_index", "analyze_architecture", "list_modules", "analyze_module",
        "analyze_file", "check_architecture_rules", "find_similar_code", "suggest_implementation",
        "search_code", "find_dependencies", "get_dependency_graph", "analyze_impact",
        "get_git_status", "get_git_diff", "get_recent_commits",
        "architecture_decision", "list_architecture_decisions", "review_change",
        "analyze_angular", "analyze_flutter", "analyze_database",
        "find_table_usage", "map_feature_across_stack", "find_sql_usage", "get_database_schema",
        "get_server_info", "get_write_policy",
        "draft_adr", "scaffold_command", "scaffold_query", "write_architecture_note",
    };

    public static IReadOnlyList<string> AllowedToolNames { get; } =
        Allowed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

    public static bool IsAllowed(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;
        if (Denied.Contains(toolName))
            return false;
        return Allowed.Contains(toolName);
    }

    public static bool RequiresTenantId(string toolName)
        => TenantScoped.Contains(toolName);

    public static bool IsDenied(string toolName)
        => Denied.Contains(toolName);
}
