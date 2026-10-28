using SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

namespace SheikhTravelSystem.Tests.AI;

public class McpAllowlistTests
{
    [Theory]
    [InlineData("list_trips")]
    [InlineData("list_fuel_records")]
    [InlineData("get_live_positions")]
    [InlineData("list_smart_alerts")]
    [InlineData("run_readonly_query")]
    [InlineData("db_connection_status")]
    [InlineData("list_tables")]
    [InlineData("describe_table")]
    [InlineData("list_vehicles")]
    [InlineData("list_drivers")]
    [InlineData("get_fleet_status")]
    [InlineData("get_approved_entity_counts")]
    [InlineData("get_database_schema")]
    [InlineData("plan_approved_query")]
    [InlineData("get_vehicle_count")]
    [InlineData("get_vehicle_details")]
    [InlineData("get_driver_details")]
    [InlineData("get_trip_summary")]
    [InlineData("get_fuel_expenses")]
    [InlineData("get_booking_summary")]
    [InlineData("get_maintenance_due")]
    [InlineData("get_revenue_summary")]
    [InlineData("get_trip_statistics")]
    [InlineData("get_vehicle_utilization")]
    [InlineData("get_fuel_cost_summary")]
    [InlineData("get_driver_performance")]
    [InlineData("get_fleet_inventory_summary")]
    [InlineData("get_gps_coverage_summary")]
    [InlineData("get_maintenance_kpi_summary")]
    [InlineData("get_booking_analytics")]
    [InlineData("get_revenue_breakdown")]
    [InlineData("get_period_comparison")]
    [InlineData("get_sales_summary")]
    [InlineData("get_top_customers_by_revenue")]
    [InlineData("get_sales_trend_comparison")]
    [InlineData("get_finance_domain")]
    [InlineData("get_finance_revenue_summary")]
    [InlineData("get_invoice_status")]
    [InlineData("get_payment_status")]
    [InlineData("get_expense_breakdown")]
    [InlineData("get_outstanding_balances")]
    [InlineData("get_analytics_domain")]
    [InlineData("get_logs_domain")]
    [InlineData("get_log_source_status")]
    [InlineData("get_diagnostic_summary")]
    [InlineData("get_ai_assistant_errors")]
    [InlineData("get_ai_usage_summary")]
    [InlineData("get_integration_diagnostics")]
    [InlineData("search_auth_failures")]
    [InlineData("get_events_by_correlation_id")]
    [InlineData("search_serilog_logs")]
    [InlineData("search_mcp_executions")]
    [InlineData("list_audit_events")]
    public void Allowlisted_reporting_tools_are_allowed(string tool)
        => Assert.True(McpAllowlist.IsAllowed(tool));

    [Theory]
    [InlineData("scaffold_command")]
    [InlineData("draft_adr")]
    [InlineData("write_architecture_note")]
    [InlineData("architecture_decision")]
    [InlineData("analyze_architecture")]
    [InlineData("search_code")]
    [InlineData("get_write_policy")]
    [InlineData("list_modules")]
    [InlineData("analyze_database")]
    [InlineData("scan_secrets")]
    [InlineData("audit_api_auth")]
    [InlineData("audit_permissions")]
    [InlineData("get_devops_domain")]
    [InlineData("get_application_health")]
    [InlineData("get_deployments")]
    [InlineData("get_build_status")]
    [InlineData("get_pipeline_status")]
    [InlineData("get_recent_ops_logs")]
    public void Architecture_write_and_file_security_tools_are_denied(string tool)
    {
        Assert.True(McpAllowlist.IsDenied(tool));
        Assert.False(McpAllowlist.IsAllowed(tool));
    }

    [Fact]
    public void Standard_chat_user_cannot_see_logs_or_audit_tools()
    {
        bool Has(string _) => false;
        Assert.True(McpAllowlist.IsPermittedForCaller("list_vehicles", Has, false));
        Assert.True(McpAllowlist.IsPermittedForCaller("get_revenue_summary", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("get_ai_assistant_errors", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("list_audit_events", Has, false));
    }

    [Fact]
    public void Provider_health_permission_unlocks_logs_tools()
    {
        bool Has(string p) => p == McpAllowlist.PermViewProviderHealth;
        Assert.True(McpAllowlist.IsPermittedForCaller("get_ai_usage_summary", Has, false));
        Assert.True(McpAllowlist.IsPermittedForCaller("search_auth_failures", Has, false));
        Assert.True(McpAllowlist.IsPermittedForCaller("get_events_by_correlation_id", Has, false));
        Assert.True(McpAllowlist.IsPermittedForCaller("get_diagnostic_summary", Has, false));
        Assert.True(McpAllowlist.IsPermittedForCaller("search_serilog_logs", Has, false));
        Assert.True(McpAllowlist.IsPermittedForCaller("search_mcp_executions", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("list_audit_events", Has, false));
    }

    [Fact]
    public void Standard_chat_user_cannot_call_phase6_correlation_or_auth_log_tools()
    {
        bool Has(string _) => false;
        Assert.False(McpAllowlist.IsPermittedForCaller("search_auth_failures", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("get_events_by_correlation_id", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("get_log_source_status", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("search_serilog_logs", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("search_mcp_executions", Has, false));
    }

    [Fact]
    public void Audit_view_permission_unlocks_audit_tools()
    {
        bool Has(string p) => p == McpAllowlist.PermAuditView;
        Assert.True(McpAllowlist.IsPermittedForCaller("list_audit_events", Has, false));
        Assert.False(McpAllowlist.IsPermittedForCaller("get_integration_diagnostics", Has, false));
    }

    [Fact]
    public void SuperAdmin_sees_all_allowlisted_tools()
    {
        bool Has(string _) => false;
        Assert.True(McpAllowlist.IsPermittedForCaller("get_ai_assistant_errors", Has, true));
        Assert.True(McpAllowlist.IsPermittedForCaller("list_audit_events", Has, true));
    }

    [Fact]
    public void Analytics_and_logs_remain_separate_allowlist_sets()
    {
        Assert.False(McpAllowlist.IsLogsTool("get_revenue_summary"));
        Assert.True(McpAllowlist.IsLogsTool("get_ai_assistant_errors"));
        Assert.True(McpAllowlist.IsAuditTool("list_audit_events"));
        Assert.False(McpAllowlist.IsAuditTool("get_trip_statistics"));
    }

    [Fact]
    public void BuildSafeArguments_overwrites_client_tenantId()
    {
        var args = SheikhGoMcpBridge.BuildSafeArguments(
            tenantId: 42,
            toolName: "list_trips",
            arguments: new Dictionary<string, object?>
            {
                ["tenantId"] = 999,
                ["status"] = "Completed"
            });

        Assert.Equal(42L, Convert.ToInt64(args["tenantId"]));
        Assert.Equal("Completed", args["status"]);
    }

    [Fact]
    public void BuildSafeArguments_rejects_destructive_sql()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SheikhGoMcpBridge.BuildSafeArguments(
                1,
                "run_readonly_query",
                new Dictionary<string, object?>
                {
                    ["sql"] = "DELETE FROM Bookings WHERE TenantId = 1"
                }));
    }

    [Fact]
    public void BuildSafeArguments_allows_select_with_forced_tenant()
    {
        var args = SheikhGoMcpBridge.BuildSafeArguments(
            7,
            "run_readonly_query",
            new Dictionary<string, object?>
            {
                ["sql"] = "SELECT TOP 10 Id FROM Bookings WHERE TenantId = @TenantId"
            });

        Assert.Equal(7L, Convert.ToInt64(args["tenantId"]));
        Assert.Equal(50, Convert.ToInt32(args["maxRows"]));
    }

    [Fact]
    public void BuildSafeArguments_hard_fails_select_without_tenant_filter()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SheikhGoMcpBridge.BuildSafeArguments(
                7,
                "run_readonly_query",
                new Dictionary<string, object?>
                {
                    ["sql"] = "SELECT TOP 10 Id FROM Bookings"
                }));

        Assert.Contains("TenantId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT Id FROM Vehicles WHERE TenantId = 1", true)]
    [InlineData("SELECT Id FROM Vehicles WHERE Id = @TenantId", true)]
    [InlineData("SELECT Id FROM Vehicles", false)]
    public void LooksLikeTenantFilter_matches_mcp_guard(string sql, bool expected)
        => Assert.Equal(expected, SheikhGoMcpBridge.LooksLikeTenantFilter(sql));
}
