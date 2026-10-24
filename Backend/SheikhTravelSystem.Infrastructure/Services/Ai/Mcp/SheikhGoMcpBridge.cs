using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

/// <summary>
/// Allowlisted MCP tool façade for SheikhGo AI Assistant. Injects tenantId from auth; denies Architecture/writes.
/// </summary>
public sealed class SheikhGoMcpBridge(
    SheikhGoMcpStdioClient client,
    IOptions<AiMcpBridgeOptions> options,
    ILogger<SheikhGoMcpBridge> logger) : ISheikhGoMcpBridge
{
    private readonly AiMcpBridgeOptions _options = options.Value;
    private IReadOnlyList<AiLlmToolDefinition>? _cachedSchemas;
    private readonly object _schemaLock = new();

    public bool IsEnabled =>
        _options.Enabled && client.IsConfigured;

    public IReadOnlyList<string> ListAllowlistedToolNames()
        => McpAllowlist.AllowedToolNames;

    public async Task<IReadOnlyList<AiLlmToolDefinition>> GetAllowlistedLlmToolsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return [];

        lock (_schemaLock)
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

            // Fallback static schemas if MCP returned nothing allowlisted
            if (schemas.Count == 0)
                schemas.AddRange(BuildStaticFallbackSchemas());

            lock (_schemaLock)
            {
                _cachedSchemas = schemas;
            }

            return schemas;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP tools/list failed; using static allowlist schemas");
            var fallback = BuildStaticFallbackSchemas();
            lock (_schemaLock)
            {
                _cachedSchemas = fallback;
            }
            return fallback;
        }
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

        try
        {
            var args = BuildSafeArguments(tenantId, toolName, arguments);

            logger.LogInformation(
                "MCP tool call tenant={TenantId} tool={Tool}",
                tenantId, toolName);

            var result = await client.CallAsync(
                "tools/call",
                new { name = toolName, arguments = args },
                cancellationToken);

            var content = SheikhGoMcpStdioClient.ExtractToolContent(result);
            var isError = result.TryGetProperty("isError", out var errFlag) &&
                          errFlag.ValueKind == JsonValueKind.True;

            if (isError)
            {
                return new McpToolCallResult(false, toolName, content, Truncate(content, 400));
            }

            return new McpToolCallResult(true, toolName, Truncate(content, 12_000));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP tool {Tool} failed", toolName);
            return new McpToolCallResult(false, toolName, string.Empty, ex.Message);
        }
    }

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
                // Never trust client/LLM tenantId
                if (kv.Key.Equals("tenantId", StringComparison.OrdinalIgnoreCase))
                    continue;
                args[kv.Key] = kv.Value;
            }
        }

        if (McpAllowlist.RequiresTenantId(toolName))
            args["tenantId"] = tenantId;

        // Extra guardrails for ad-hoc SQL
        if (toolName.Equals("run_readonly_query", StringComparison.OrdinalIgnoreCase))
        {
            args["tenantId"] = tenantId;
            if (!args.ContainsKey("maxRows"))
                args["maxRows"] = 50;

            if (args.TryGetValue("sql", out var sqlObj) && sqlObj is string sql)
            {
                var upper = sql.TrimStart().ToUpperInvariant();
                if (upper.StartsWith("INSERT") || upper.StartsWith("UPDATE") ||
                    upper.StartsWith("DELETE") || upper.StartsWith("DROP") ||
                    upper.StartsWith("ALTER") || upper.StartsWith("CREATE") ||
                    upper.StartsWith("EXEC") || upper.StartsWith("MERGE") ||
                    upper.Contains(";"))
                {
                    throw new InvalidOperationException(
                        "Destructive or multi-statement SQL is not allowed via SheikhGo AI Assistant MCP bridge.");
                }
            }
        }

        return args;
    }

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

        // Hide tenantId from the LLM schema — bridge injects it
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
                $"SheikhGo MCP read-only tool: {name}. Tenant scope is applied by the server.",
                EmptyObj()))
            .ToList();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
