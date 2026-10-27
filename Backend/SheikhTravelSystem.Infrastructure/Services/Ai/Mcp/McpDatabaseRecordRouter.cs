using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// Maps “show me records / list fleet / subscription modules / table schema” questions to
/// allowlisted SheikhGo Database MCP tools (and domain wrappers that use the same live SELECT path).
/// </summary>
public sealed class McpDatabaseRecordRouter(
    ISheikhGoMcpBridge mcpBridge,
    ILogger<McpDatabaseRecordRouter> logger) : IMcpDatabaseRecordRouter
{
    private static readonly Regex TableNameHint = new(
        @"\b(?:from|table|into)\s+([A-Za-z][A-Za-z0-9_]{1,64})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public bool LooksLikeRecordQuery(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        var q = question.Trim().ToLowerInvariant();
        return ResolveIntents(q).Count > 0;
    }

    public async Task<McpRecordFetchResult?> TryFetchAsync(
        int tenantId,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0 || !mcpBridge.IsEnabled)
            return null;

        var q = question?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(q))
            return null;

        var intents = ResolveIntents(q.ToLowerInvariant());
        if (intents.Count == 0)
            return null;

        var toolsUsed = new List<string>();
        var parts = new StringBuilder();
        var anySuccess = false;

        foreach (var intent in intents.Take(2))
        {
            logger.LogInformation(
                "MCP record router tenant={TenantId} tool={Tool} intent={Intent}",
                tenantId, intent.ToolName, intent.Label);

            var result = await mcpBridge.CallAsync(
                tenantId, intent.ToolName, intent.Arguments, cancellationToken);

            toolsUsed.Add($"mcp:{intent.ToolName}");
            if (result.Success && !string.IsNullOrWhiteSpace(result.Content))
            {
                anySuccess = true;
                parts.AppendLine($"[{intent.ToolName} — {intent.Label}]");
                parts.AppendLine(Truncate(result.Content, 6_000));
                parts.AppendLine();
            }
            else
            {
                parts.AppendLine(
                    $"[{intent.ToolName}] unavailable: {result.Error ?? "no data"}");
            }
        }

        if (!anySuccess && parts.Length == 0)
            return null;

        return new McpRecordFetchResult(
            parts.ToString().Trim(),
            toolsUsed,
            anySuccess);
    }

    internal static List<McpIntent> ResolveIntents(string q)
    {
        var intents = new List<McpIntent>();

        // Subscription / entitlement module rows (DB) — not Architecture list_modules
        if (ContainsAny(q,
                "subscription module", "subscription modules", "subscribed modules",
                "enabled modules", "module entitlement", "tenant modules",
                "company modules", "licensed modules", "modules enabled", "modules are on",
                "which modules", "what modules",
                "सब्सक्रिप्शन", "मॉड्यूल", "سبسکرپشن", "ماڈیول",
                "tenantmodules", "from modules"))
        {
            intents.Add(new McpIntent(
                "run_readonly_query",
                "subscription modules (live SELECT)",
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sql"] = """
                        SELECT TOP 50 m.ModuleCode, m.Name, m.Category, tm.TenantId
                        FROM TenantModules tm
                        INNER JOIN Modules m ON m.Id = tm.ModuleId
                        WHERE tm.TenantId = @TenantId
                        ORDER BY m.ModuleCode
                        """,
                    ["maxRows"] = 50
                }));
            return intents;
        }

        // Schema / table exploration
        if (ContainsAny(q, "list tables", "show tables", "what tables", "database tables", "schema tables"))
        {
            intents.Add(new McpIntent("list_tables", "list live tables", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q, "describe table", "table columns", "column list", "table schema"))
        {
            var table = ExtractTableName(q) ?? "Vehicles";
            intents.Add(new McpIntent(
                "describe_table",
                $"describe {table}",
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["table"] = table
                }));
            return intents;
        }

        if (ContainsAny(q, "db connection", "database connected", "mcp database", "live database status"))
        {
            intents.Add(new McpIntent("db_connection_status", "DB connection status", EmptyArgs()));
            return intents;
        }

        // Phase 4: vehicle count vs list
        if (ContainsAny(q,
                "how many vehicles", "how many vehicle", "vehicle count", "count vehicles",
                "number of vehicles", "total vehicles", "vehicles do we have", "vehicles do i have"))
        {
            intents.Add(new McpIntent("get_vehicle_count", "vehicle count", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "list vehicles", "show vehicles", "all vehicles", "vehicle list", "vehicles list",
                "show me vehicles", "fetch vehicles", "get vehicles", "vehicle records"))
        {
            intents.Add(new McpIntent("list_vehicles", "vehicle records", PageArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "gps coverage", "stale location", "stale gps", "gps reporting",
                "how many gps", "location freshness", "devices reporting"))
        {
            intents.Add(new McpIntent("get_gps_coverage_summary", "GPS coverage KPIs", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "offline vehicles", "vehicles offline", "which vehicles are offline",
                "gps offline", "not reporting"))
        {
            intents.Add(new McpIntent("get_fleet_status", "fleet / offline status", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "active vehicles", "inactive vehicles", "fleet inventory",
                "total active vehicles", "how many active"))
        {
            intents.Add(new McpIntent("get_fleet_inventory_summary", "fleet inventory KPIs", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "list drivers", "show drivers", "all drivers", "driver list", "drivers list",
                "show me drivers", "fetch drivers", "get drivers", "driver records"))
        {
            intents.Add(new McpIntent("list_drivers", "driver records", PageArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "fleet status", "vehicles on trip", "vehicles on trips", "assignments",
                "vehicle assignment", "list assignments"))
        {
            if (ContainsAny(q, "assignment", "assignments"))
                intents.Add(new McpIntent("list_vehicle_assignments", "assignments", PageArgs()));
            else if (ContainsAny(q, "on trip", "on trips"))
                intents.Add(new McpIntent("get_vehicles_on_trips", "vehicles on trips", EmptyArgs()));
            else
                intents.Add(new McpIntent("get_fleet_status", "fleet status", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "maintenance overdue count", "how many overdue maintenance",
                "maintenance kpi", "maintenance counts", "due and overdue"))
        {
            intents.Add(new McpIntent("get_maintenance_kpi_summary", "maintenance KPI counts", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q, "maintenance due", "service due", "overdue maintenance", "due maintenance"))
        {
            intents.Add(new McpIntent("get_maintenance_due", "maintenance due", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "week over week", "month over month", "wow comparison", "mom comparison",
                "compare to last week", "compare to last month", "vs last week", "vs last month"))
        {
            var mode = ContainsAny(q, "month", "mom") ? "mom" : "wow";
            intents.Add(new McpIntent(
                "get_period_comparison",
                $"period comparison ({mode})",
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mode"] = mode
                }));
            return intents;
        }

        if (ContainsAny(q,
                "revenue by branch", "revenue by vehicle", "revenue by customer",
                "revenue by date", "revenue breakdown", "income by branch"))
        {
            var groupBy =
                ContainsAny(q, "branch") ? "branch" :
                ContainsAny(q, "customer") ? "customer" :
                ContainsAny(q, "vehicle") ? "vehicle" : "date";
            intents.Add(new McpIntent(
                "get_revenue_breakdown",
                $"revenue by {groupBy}",
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["groupBy"] = groupBy
                }));
            return intents;
        }

        if (ContainsAny(q,
                "fuel expense", "fuel expenses", "fuel cost", "fuel spend", "fuel summary",
                "highest fuel"))
        {
            intents.Add(new McpIntent("get_fuel_expenses", "fuel expenses", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q, "fuel records", "list fuel", "fuel log", "fuel logs", "show fuel"))
        {
            intents.Add(new McpIntent("list_fuel_records", "fuel records", PageArgs()));
            return intents;
        }

        if (ContainsAny(q, "revenue", "paid payments", "earnings", "income summary"))
        {
            intents.Add(new McpIntent("get_revenue_summary", "revenue summary", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q, "booking summary", "bookings summary", "booking stats", "booking status summary"))
        {
            intents.Add(new McpIntent("get_booking_summary", "booking summary", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q, "trip summary", "trips summary", "trip stats", "trip status summary"))
        {
            intents.Add(new McpIntent("get_trip_summary", "trip summary", EmptyArgs()));
            return intents;
        }

        // Bookings / trips / routes
        if (ContainsAny(q,
                "list bookings", "show bookings", "all bookings", "booking list", "booking records",
                "show me bookings", "fetch bookings"))
        {
            intents.Add(new McpIntent("list_bookings", "booking records", PageArgs()));
            return intents;
        }

        if (ContainsAny(q,
                "list trips", "show trips", "all trips", "trip list", "trip records",
                "show me trips", "fetch trips", "active trips"))
        {
            intents.Add(new McpIntent("list_trips", "trip records", PageArgs()));
            return intents;
        }

        if (ContainsAny(q, "list routes", "show routes", "all routes", "route list"))
        {
            intents.Add(new McpIntent("list_routes", "route records", PageArgs()));
            return intents;
        }

        // GPS live positions (lat/lng from DB — no Maps keys)
        if (ContainsAny(q,
                "live positions", "gps positions", "vehicle positions", "show positions",
                "where are vehicles", "live gps"))
        {
            intents.Add(new McpIntent("get_live_positions", "live GPS positions", EmptyArgs()));
            return intents;
        }

        if (ContainsAny(q, "smart alerts", "list alerts", "show alerts", "operational alerts"))
        {
            intents.Add(new McpIntent("list_smart_alerts", "smart alerts", EmptyArgs()));
            return intents;
        }

        // Generic “show me records” / SELECT-shaped asks → curated safe SELECT when table known
        if (ContainsAny(q, "show me records", "show records", "fetch records", "get records",
                "query records", "select from", "rows from", "data from"))
        {
            var table = ExtractTableName(q);
            if (table is not null && IsSafeTableName(table))
            {
                intents.Add(new McpIntent(
                    "run_readonly_query",
                    $"records from {table}",
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sql"] =
                            $"SELECT TOP 50 * FROM [{table}] WHERE TenantId = @TenantId",
                        ["maxRows"] = 50
                    }));
            }
            else
            {
                // Discover tables first so the LLM / user can refine
                intents.Add(new McpIntent("list_tables", "discover tables for record query", EmptyArgs()));
            }

            return intents;
        }

        return intents;
    }

    private static Dictionary<string, object?> EmptyArgs()
        => new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, object?> PageArgs()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = 1,
            ["pageSize"] = 25
        };

    private static string? ExtractTableName(string q)
    {
        var m = TableNameHint.Match(q);
        if (m.Success)
            return m.Groups[1].Value;

        // "describe Vehicles" / "Vehicles table"
        m = Regex.Match(q, @"\bdescribe\s+([A-Za-z][A-Za-z0-9_]{1,64})\b", RegexOptions.IgnoreCase);
        if (m.Success)
            return m.Groups[1].Value;

        m = Regex.Match(q, @"\b([A-Za-z][A-Za-z0-9_]{1,64})\s+table\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static bool IsSafeTableName(string name)
        => Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]{0,64}$");

    private static bool ContainsAny(string q, params string[] tokens)
        => tokens.Any(t => q.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    internal sealed record McpIntent(
        string ToolName,
        string Label,
        IReadOnlyDictionary<string, object?> Arguments);
}
