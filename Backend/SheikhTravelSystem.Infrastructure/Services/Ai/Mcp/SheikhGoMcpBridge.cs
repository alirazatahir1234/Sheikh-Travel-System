using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// Allowlisted MCP tool façade for SheikhGo AI Assistant.
/// Injects tenantId from auth; denies Architecture/writes/file scanners;
/// filters elevated Logs/Audit tools by caller permissions (Phase 3).
/// </summary>
public sealed class SheikhGoMcpBridge(
    SheikhGoMcpStdioClient client,
    IOptions<AiMcpBridgeOptions> options,
    ICurrentUserService currentUser,
    IMcpToolExecutionLogger executionLogger,
    IMcpExecutionAmbient executionAmbient,
    ILogger<SheikhGoMcpBridge> logger) : ISheikhGoMcpBridge
{
    private readonly AiMcpBridgeOptions _options = options.Value;
    // Process-wide schema cache (bridge is scoped for ICurrentUserService; stdio client is singleton).
    private static IReadOnlyList<AiLlmToolDefinition>? _cachedSchemas;
    private static readonly object SchemaLock = new();

    public bool IsEnabled =>
        _options.Enabled && client.IsConfigured;

    public IReadOnlyList<string> ListAllowlistedToolNames()
        => McpAllowlist.AllowedToolNames
            .Where(IsPermitted)
            .ToList();

    public async Task<IReadOnlyList<AiLlmToolDefinition>> GetAllowlistedLlmToolsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return [];

        var all = await LoadAllAllowlistedSchemasAsync(cancellationToken);
        return all.Where(t => IsPermitted(t.Name)).ToList();
    }

    public async Task<McpToolCallResult> CallAsync(
        int tenantId,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
            return new McpToolCallResult(false, toolName, string.Empty, "Invalid tenant.");

        if (!IsEnabled)
            return new McpToolCallResult(false, toolName, string.Empty, "MCP bridge disabled or not configured.");

        if (string.IsNullOrWhiteSpace(toolName))
            return new McpToolCallResult(false, toolName, string.Empty, "Tool name required.");

        if (McpAllowlist.IsDenied(toolName) || !McpAllowlist.IsAllowed(toolName))
        {
            logger.LogWarning("Denied MCP tool call attempt: {Tool} tenant={TenantId}", toolName, tenantId);
            return new McpToolCallResult(
                false, toolName, string.Empty,
                $"Tool '{toolName}' is not allowlisted for tenant AI Assistant (ADR-AI-001).");
        }

        if (!IsPermitted(toolName))
        {
            var gate = McpAllowlist.DescribePermissionGate(toolName) ?? "elevated permission";
            logger.LogWarning(
                "Permission denied MCP tool {Tool} tenant={TenantId} required={Gate}",
                toolName, tenantId, gate);
            return new McpToolCallResult(
                false, toolName, string.Empty,
                $"Tool '{toolName}' requires {gate}.");
        }

        // Phase 8 — reuse turn-scoped correlation id from ambient (gateway / loop). Do not mint a new turn id here.
        var correlationId = executionAmbient.EnsureCorrelationId();
        var callIndex = ++executionAmbient.ToolCallIndex;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Dictionary<string, object?>? safeArgs = null;
        try
        {
            safeArgs = BuildSafeArguments(tenantId, toolName, arguments);

            logger.LogInformation(
                "MCP tool call tenant={TenantId} tool={Tool} correlationId={CorrelationId} callIndex={CallIndex} session={SessionId}",
                tenantId, toolName, correlationId, callIndex, executionAmbient.ChatSessionId);

            var result = await client.CallAsync(
                "tools/call",
                new { name = toolName, arguments = safeArgs },
                cancellationToken);

            var content = SheikhGoMcpStdioClient.ExtractToolContent(result);
            var isError = result.TryGetProperty("isError", out var errFlag) &&
                          errFlag.ValueKind == JsonValueKind.True;

            sw.Stop();
            executionAmbient.McpLatencyMs += sw.ElapsedMilliseconds;
            if (isError)
            {
                var err = Truncate(content, 400);
                await TryLogExecutionAsync(
                    tenantId, toolName, success: false, sw, correlationId, safeArgs, err, cancellationToken);
                return new McpToolCallResult(false, toolName, content, err);
            }

            await TryLogExecutionAsync(
                tenantId, toolName, success: true, sw, correlationId, safeArgs, error: null, cancellationToken);
            return new McpToolCallResult(true, toolName, Truncate(content, 12_000));
        }
        catch (Exception ex)
        {
            sw.Stop();
            executionAmbient.McpLatencyMs += sw.ElapsedMilliseconds;
            logger.LogWarning(
                ex,
                "MCP tool {Tool} failed correlationId={CorrelationId} callIndex={CallIndex} durationMs={DurationMs}",
                toolName, correlationId, callIndex, sw.ElapsedMilliseconds);
            await TryLogExecutionAsync(
                tenantId, toolName, success: false, sw, correlationId, safeArgs, Truncate(ex.Message, 400),
                cancellationToken);
            return new McpToolCallResult(false, toolName, string.Empty, ex.Message);
        }
    }

    private async Task TryLogExecutionAsync(
        int tenantId,
        string toolName,
        bool success,
        System.Diagnostics.Stopwatch sw,
        string correlationId,
        IReadOnlyDictionary<string, object?>? args,
        string? error,
        CancellationToken cancellationToken)
    {
        try
        {
            await executionLogger.LogAsync(
                new McpToolExecutionLogEntry(
                    tenantId,
                    toolName,
                    success,
                    (int)Math.Min(sw.ElapsedMilliseconds, int.MaxValue),
                    correlationId,
                    executionAmbient.ChatSessionId,
                    currentUser.UserId,
                    McpToolExecutionLogger.SanitizeArgsPreview(args),
                    McpToolExecutionLogger.Sanitize(error)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "MCP execution log write failed for {Tool}", toolName);
        }
    }

    private bool IsPermitted(string toolName)
        => McpAllowlist.IsPermittedForCaller(
            toolName,
            currentUser.HasPermission,
            currentUser.IsPlatformSuperAdmin);

    private async Task<IReadOnlyList<AiLlmToolDefinition>> LoadAllAllowlistedSchemasAsync(
        CancellationToken cancellationToken)
    {
        lock (SchemaLock)
        {
            if (_cachedSchemas is not null)
                return _cachedSchemas;
        }

        try
        {
            var result = await client.CallAsync("tools/list", new { }, cancellationToken);
            var parsed = SheikhGoMcpStdioClient.ParseToolsList(result);
            var schemas = new List<AiLlmToolDefinition>();

            foreach (var (name, description, inputSchema) in parsed)
            {
                if (!McpAllowlist.IsAllowed(name))
                    continue;

                var schema = SanitizeSchemaForTenant(name, inputSchema);
                schemas.Add(new AiLlmToolDefinition(name, description, schema));
            }

            if (schemas.Count == 0)
                schemas.AddRange(BuildStaticFallbackSchemas());

            lock (SchemaLock)
            {
                _cachedSchemas = schemas;
            }

            return schemas;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP tools/list failed; using static allowlist schemas");
            var fallback = BuildStaticFallbackSchemas();
            lock (SchemaLock)
            {
                _cachedSchemas = fallback;
            }
            return fallback;
        }
    }

    /// <summary>Hard ceiling for page/limit/maxRows from the model (Phase 8).</summary>
    public const int MaxResultLimit = 200;

    internal static Dictionary<string, object?> BuildSafeArguments(
        int tenantId,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (arguments is not null)
        {
            foreach (var kv in arguments)
            {
                // Trusted tenant comes only from auth — strip forged company/tenant keys.
                if (IsForbiddenTenantKey(kv.Key))
                    continue;
                args[kv.Key] = kv.Value;
            }
        }

        if (McpAllowlist.RequiresTenantId(toolName))
            args["tenantId"] = tenantId;

        ClampLimitArg(args, "limit");
        ClampLimitArg(args, "pageSize");
        ClampLimitArg(args, "top");
        ClampLimitArg(args, "maxRows");

        if (toolName.Equals("run_readonly_query", StringComparison.OrdinalIgnoreCase))
        {
            args["tenantId"] = tenantId;
            if (!args.ContainsKey("maxRows"))
                args["maxRows"] = 50;
            else
                ClampLimitArg(args, "maxRows");
            // Phase 4: AI bridge forces approved-table validation (Cursor can omit approvedOnly).
            args["approvedOnly"] = true;

            if (!args.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql ||
                string.IsNullOrWhiteSpace(sql))
            {
                throw new InvalidOperationException(
                    "run_readonly_query requires a non-empty sql string.");
            }

            var upper = sql.TrimStart().ToUpperInvariant();
            if (upper.StartsWith("INSERT") || upper.StartsWith("UPDATE") ||
                upper.StartsWith("DELETE") || upper.StartsWith("DROP") ||
                upper.StartsWith("ALTER") || upper.StartsWith("CREATE") ||
                upper.StartsWith("EXEC") || upper.StartsWith("MERGE") ||
                upper.Contains(';'))
            {
                throw new InvalidOperationException(
                    "Destructive or multi-statement SQL is not allowed via SheikhGo AI Assistant MCP bridge.");
            }

            if (!LooksLikeTenantFilter(sql))
            {
                throw new InvalidOperationException(
                    "run_readonly_query rejected: SQL must reference TenantId or @TenantId " +
                    "(tenant isolation). Add WHERE TenantId = @TenantId.");
            }
        }

        return args;
    }

    internal static bool IsForbiddenTenantKey(string key)
        => key.Equals("tenantId", StringComparison.OrdinalIgnoreCase)
           || key.Equals("tenant_id", StringComparison.OrdinalIgnoreCase)
           || key.Equals("companyId", StringComparison.OrdinalIgnoreCase)
           || key.Equals("company_id", StringComparison.OrdinalIgnoreCase)
           || key.Equals("targetTenantId", StringComparison.OrdinalIgnoreCase);

    internal static void ClampLimitArg(IDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var raw) || raw is null)
            return;

        try
        {
            var n = Convert.ToInt32(raw);
            args[key] = Math.Clamp(n, 1, MaxResultLimit);
        }
        catch
        {
            args[key] = Math.Min(50, MaxResultLimit);
        }
    }

    internal static bool LooksLikeTenantFilter(string sql)
        => TenantIdToken.IsMatch(sql);

    private static readonly Regex TenantIdToken = new(
        @"\bTenantId\b|@TenantId\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static object SanitizeSchemaForTenant(string toolName, JsonNode? inputSchema)
    {
        if (inputSchema is null)
        {
            return McpAllowlist.RequiresTenantId(toolName)
                ? new
                {
                    type = "object",
                    properties = new { },
                    additionalProperties = true
                }
                : new { type = "object", properties = new { } };
        }

        if (inputSchema is JsonObject obj &&
            obj["properties"] is JsonObject props &&
            props.ContainsKey("tenantId"))
        {
            props.Remove("tenantId");
            if (obj["required"] is JsonArray req)
            {
                for (var i = req.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(req[i]?.GetValue<string>(), "tenantId", StringComparison.OrdinalIgnoreCase))
                        req.RemoveAt(i);
                }
            }
        }

        return JsonSerializer.Deserialize<object>(inputSchema.ToJsonString()) ?? new { type = "object" };
    }

    private static List<AiLlmToolDefinition> BuildStaticFallbackSchemas()
    {
        object EmptyObj() => new
        {
            type = "object",
            properties = new Dictionary<string, object>(),
            additionalProperties = true
        };

        return McpAllowlist.AllowedToolNames
            .Select(name => new AiLlmToolDefinition(
                name,
                StaticToolDescription(name),
                EmptyObj()))
            .ToList();
    }

    private static string StaticToolDescription(string name) => name.ToLowerInvariant() switch
    {
        "run_readonly_query" =>
            "Last-resort read-only SELECT (tenant-scoped). Prefer Phase 4 tools (get_vehicle_count, get_trip_summary, …).",
        "get_approved_entity_counts" =>
            "Predefined COUNT(*) for approved tenant tables (vehicles, drivers, bookings, trips, …).",
        "get_database_schema" =>
            "Approved business schema catalog (tables/columns/joins/gaps) for factual AI retrieval.",
        "plan_approved_query" =>
            "Map a natural-language question to an approved Phase 4 tool; rejects unsafe plans.",
        "get_vehicle_count" =>
            "Tenant vehicle count with status breakdown (default excludes Draft/Retired).",
        "get_vehicle_details" =>
            "One vehicle by id; found=false when missing (empty success).",
        "get_driver_details" =>
            "One driver by id; found=false when missing (empty success).",
        "get_trip_summary" =>
            "Trip status histogram for a date period (TripDate).",
        "get_fuel_expenses" =>
            "Fuel cost and liters by vehicle for a period (FuelLogs).",
        "get_booking_summary" =>
            "Booking status rollup for a period (PickupTime).",
        "list_tables" =>
            "List live SQL tables/views. Use before guessing table names for record queries.",
        "describe_table" =>
            "Describe columns for one live table. Use before SELECT.",
        "db_connection_status" =>
            "Check whether the Database MCP live connection is configured and reachable.",
        "get_revenue_summary" =>
            "Paid revenue totals (daily / weekly / monthly). Analytics KPI — not Logs.",
        "get_trip_statistics" =>
            "Trip and booking analytics (responseType=trip_booking_analytics: KPIs, trends, recent trips).",
        "get_vehicle_utilization" =>
            "Fleet utilization snapshot and top vehicles by GPS trip distance.",
        "get_fuel_cost_summary" =>
            "Fuel cost and liters by vehicle for a date range (fleet totals via SQL SUM).",
        "get_driver_performance" =>
            "Drivers ranked by completed trips in a date range.",
        "get_fleet_inventory_summary" =>
            "Total / active / inactive vehicle counts (Phase 5 Analytics).",
        "get_gps_coverage_summary" =>
            "GPS device coverage and fresh vs stale location counts (Phase 5).",
        "get_maintenance_kpi_summary" =>
            "Maintenance overdue / due counts for work-orders and schedules (Phase 5).",
        "get_booking_analytics" =>
            "Booking totals and status breakdown by PickupTime (Phase 5).",
        "get_revenue_breakdown" =>
            "Paid revenue by date, branch, vehicle, or customer (Phase 5).",
        "get_period_comparison" =>
            "Week-over-week or month-over-month KPI comparison (Phase 5).",
        "get_sales_summary" =>
            "Paid sales/revenue summary for today / week / month (responseType=sales_analytics).",
        "get_top_customers_by_revenue" =>
            "Top customers by paid revenue for a period (responseType=sales_analytics).",
        "get_sales_trend_comparison" =>
            "Sales WoW/MoM trend comparison with daily series (responseType=sales_analytics).",
        "get_analytics_domain" =>
            "Analytics KPI catalog including unavailable metrics (no fleet-health %).",
        "get_logs_domain" =>
            "Phase 6 Logs catalog: existing vs unavailable diagnostic sources (elevated).",
        "get_log_source_status" =>
            "Which log sinks exist (Serilog files, McpToolExecutions, …) vs unavailable (job ledger, Seq).",
        "get_diagnostic_summary" =>
            "Structured diagnostic rollup + unavailable capabilities (elevated).",
        "get_ai_assistant_errors" =>
            "Recent AI TOOL_ERROR / provider failure markers, redacted (elevated).",
        "get_ai_usage_summary" =>
            "AiUsageLedger token/feature rollup (diagnostics — not revenue).",
        "get_integration_diagnostics" =>
            "Database + Traccar connectivity and GPS freshness sample.",
        "search_auth_failures" =>
            "AuditEvents auth/authorization failures with CorrelationId when present (elevated).",
        "get_events_by_correlation_id" =>
            "AuditEvents sharing a CorrelationId — empty if missing; never invent trails.",
        "search_serilog_logs" =>
            "Bounded Serilog rolling-file search (Logs/log-.txt). Redacted. No shell.",
        "search_mcp_executions" =>
            "Query McpToolExecutions by tool/success/correlationId/session (elevated).",
        "list_audit_events" =>
            "Tenant AuditLogs for authorized users (elevated).",
        "list_vehicles" or "list_drivers" or "list_bookings" or "list_trips" or "list_routes"
            or "list_fuel_records" or "list_vehicle_assignments" =>
            $"List live {name.Replace("list_", "", StringComparison.Ordinal)} records for the current tenant.",
        "get_fleet_status" =>
            "Live fleet status buckets (online/offline/moving) from GPS + vehicles.",
        "get_live_positions" =>
            "Live vehicle lat/lng from SheikhGo GPS tables (no Maps API keys).",
        _ =>
            $"SheikhGo MCP read-only tool: {name}. Tenant scope is applied by the server. Prefer for live business records over inventing data."
    };

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
