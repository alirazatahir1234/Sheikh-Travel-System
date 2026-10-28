namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// ADR-AI-001: tenant SheikhGo AI Assistant may call reporting/read-only MCP groups only.
/// Architecture scaffolds, git, code search, controlled writes, and file-based security scanners are denied.
/// <para>
/// Phase 3 catalog: Database, Bookings, Fleet, GPS, SmartOps, Analytics, Logs (elevated), Security audit events (elevated).
/// Phase 4: canonical business retrieval tools (vehicle/driver/trip/fuel/booking/revenue/schema) + approved SQL planner.
/// Phase 5: Analytics KPI hardening (fleet inventory, GPS coverage, maintenance KPIs, revenue breakdown, WoW/MoM).
/// Phase 6: Logs / operational diagnostics (assistant errors, auth failures, correlation, integration; redacted).
/// Analytics ≠ Logs. Sensitive tools require matching permissions on the authenticated user.
/// </para>
/// </summary>
public static class McpAllowlist
{
    /// <summary>Permission codes (must match <c>AiPermissions</c> / <c>PlatformPermissions</c>).</summary>
    public const string PermChat = "Ai.Chat";
    public const string PermViewProviderHealth = "Ai.ViewProviderHealth";
    public const string PermAiManage = "Ai.Manage";
    public const string PermAuditView = "Platform.Audit.View";
    public const string PermAuditLogsView = "Platform.AuditLogs.View";

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
        // Phase 4 business retrieval
        "get_vehicle_count", "get_vehicle_details", "get_driver_details",
        "get_trip_summary", "get_fuel_expenses", "get_booking_summary",
        // GPS / Traccar
        "get_devices", "get_live_positions", "get_vehicle_history",
        "get_geofences", "get_geofence_events", "get_gps_trips",
        // SmartOps
        "list_smart_alerts", "get_maintenance_reminders", "get_trip_delays",
        // Database
        "run_readonly_query", "get_approved_entity_counts",
        // Analytics (+ Phase 5)
        "get_revenue_summary", "get_trip_statistics", "get_vehicle_utilization",
        "get_fuel_cost_summary", "get_driver_performance",
        "get_fleet_inventory_summary", "get_gps_coverage_summary",
        "get_maintenance_kpi_summary", "get_booking_analytics",
        "get_revenue_breakdown", "get_period_comparison",
        "get_sales_summary", "get_top_customers_by_revenue", "get_sales_trend_comparison",
        // Logs Phase 6 (elevated)
        "get_ai_assistant_errors", "get_ai_usage_summary", "get_integration_diagnostics",
        "get_diagnostic_summary", "search_auth_failures", "get_events_by_correlation_id",
        "search_mcp_executions",
        // Security audit (elevated)
        "list_audit_events",
    };

    /// <summary>Standard business tools — any user with Ai.Chat (and AI module).</summary>
    private static readonly HashSet<string> Standard = new(StringComparer.OrdinalIgnoreCase)
    {
        // Database
        "db_connection_status",
        "list_tables",
        "describe_table",
        "run_readonly_query",
        "get_approved_entity_counts",
        "get_database_schema",
        "plan_approved_query",
        // Phase 4 canonical business tools
        "get_vehicle_count",
        "get_vehicle_details",
        "get_driver_details",
        "get_trip_summary",
        "get_fuel_expenses",
        "get_booking_summary",
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
        // GPS / Traccar
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
        // Analytics (business KPIs — not Logs) + Phase 5
        "get_analytics_domain",
        "get_revenue_summary",
        "get_trip_statistics",
        "get_vehicle_utilization",
        "get_fuel_cost_summary",
        "get_driver_performance",
        "get_fleet_inventory_summary",
        "get_gps_coverage_summary",
        "get_maintenance_kpi_summary",
        "get_booking_analytics",
        "get_revenue_breakdown",
        "get_period_comparison",
        "get_sales_summary",
        "get_top_customers_by_revenue",
        "get_sales_trend_comparison",
    };

    /// <summary>Logs MCP Phase 6 — operational diagnostics (not business analytics).</summary>
    private static readonly HashSet<string> LogsElevated = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_logs_domain",
        "get_log_source_status",
        "get_diagnostic_summary",
        "get_ai_assistant_errors",
        "get_ai_usage_summary",
        "get_integration_diagnostics",
        "search_auth_failures",
        "get_events_by_correlation_id",
        "search_serilog_logs",
        "search_mcp_executions",
    };

    /// <summary>Security/Audit tenant events (not file scanners).</summary>
    private static readonly HashSet<string> AuditElevated = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_security_domain",
        "list_audit_events",
    };

    private static readonly HashSet<string> Allowed = new(
        Standard.Concat(LogsElevated).Concat(AuditElevated),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Hard deny — Architecture / writes / code / file security scanners (never callable from tenant SheikhGo AI Assistant).</summary>
    private static readonly HashSet<string> Denied = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_repository_index", "analyze_architecture", "list_modules", "analyze_module",
        "analyze_file", "check_architecture_rules", "find_similar_code", "suggest_implementation",
        "search_code", "find_dependencies", "get_dependency_graph", "analyze_impact",
        "get_git_status", "get_git_diff", "get_recent_commits",
        "architecture_decision", "list_architecture_decisions", "review_change",
        "analyze_angular", "analyze_flutter", "analyze_database",
        "find_table_usage", "map_feature_across_stack", "find_sql_usage",
        "get_server_info", "get_write_policy",
        "draft_adr", "scaffold_command", "scaffold_query", "write_architecture_note",
        // File-based Security MCP — Cursor/dev only (repo secrets / API surface). Not tenant AI.
        "scan_secrets", "audit_permissions", "audit_api_auth",
        // DevOps MCP — Cursor/dev only (health/pipeline/ops logs). Not tenant AI.
        "get_devops_domain", "get_application_health", "get_deployments",
        "get_build_status", "get_pipeline_status", "get_recent_ops_logs",
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

    public static bool IsLogsTool(string toolName)
        => LogsElevated.Contains(toolName);

    public static bool IsAuditTool(string toolName)
        => AuditElevated.Contains(toolName);

    /// <summary>
    /// Whether the authenticated caller may see/execute this allowlisted tool.
    /// Fail-closed for elevated Logs / Audit tools.
    /// </summary>
    public static bool IsPermittedForCaller(
        string toolName,
        Func<string, bool> hasPermission,
        bool isPlatformSuperAdmin)
    {
        if (!IsAllowed(toolName))
            return false;

        if (isPlatformSuperAdmin)
            return true;

        if (IsLogsTool(toolName))
        {
            return hasPermission(PermViewProviderHealth) || hasPermission(PermAiManage);
        }

        if (IsAuditTool(toolName))
        {
            return hasPermission(PermAuditView) ||
                   hasPermission(PermAuditLogsView) ||
                   hasPermission(PermAiManage);
        }

        // Standard catalog — chat permission is enforced at the API controller;
        // bridge still requires the tool to be allowlisted.
        return true;
    }

    public static string? DescribePermissionGate(string toolName)
    {
        if (IsLogsTool(toolName))
            return $"{PermViewProviderHealth} or {PermAiManage}";
        if (IsAuditTool(toolName))
            return $"{PermAuditView} / {PermAuditLogsView} or {PermAiManage}";
        return null;
    }
}
