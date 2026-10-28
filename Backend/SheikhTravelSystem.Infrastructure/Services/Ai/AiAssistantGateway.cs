using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

/// <summary>
/// Phase 2 AI Assistant orchestrator: allowlisted read-only tools only.
/// When keyword IAiTools miss (or fail) and Ai:McpBridge is on, uses the same
/// DeepInfra ↔ MCP tool-calling loop as chat (<see cref="IAiMcpToolCallingLoop"/>),
/// then deterministic <see cref="IMcpDatabaseRecordRouter"/> prefetch, then help.
/// LLM (if configured) may polish wording; it never supplies tenant ID or invents SQL.
/// </summary>
public sealed class AiAssistantGateway(
    IEnumerable<IAiTool> tools,
    IAiProviderResolver providerResolver,
    IAiManagementService aiManagement,
    IMcpDatabaseRecordRouter mcpRecordRouter,
    IAiMcpToolCallingLoop mcpToolLoop,
    IAiConversationMemoryService conversationMemory,
    IAiConversationContextBuilder contextBuilder,
    IMcpExecutionAmbient executionAmbient,
    ILogger<AiAssistantGateway> logger) : IAiAssistantGateway
{
    public static readonly string[] AllowlistedToolNames =
    [
        "GetFuelCostSummary",
        "GetVehicleMaintenanceSummary",
        "GetOverdueInvoices",
        "GetTripSummary",
        "GetVehicleUtilizationSummary",
        // Operational GPS / fleet status (owner-facing)
        "GetOfflineVehicles",
        "GetFleetHealth",
        // Batch 1 (+ sales analytics parity with MCP)
        "GetRevenueAnalytics",
        "GetTopCustomersByRevenue",
        "GetSalesTrendComparison",
        "GetFuelUsageTopVehicle",
        "GetBestDriverPerformance",
        // Batch 2
        "GetCostPerKilometer",
        "GetOverspeedGeofenceSummary",
        // Batch 3
        "GetIdleTimeReport",
        "GetSosEventsSummary",
        "GetExpenseTracking",
        "GetTenantComparison",
        "GetPeakHoursAnalysis",
        // Phase 3 — Predictive Intelligence
        "GetMaintenanceRiskForecast",
        "GetTripDelayRisk",
        "GetFuelAnomalyInvestigation",
        // Platform / subscription entitlements (not MCP list_modules)
        "GetTenantSubscriptionModules",
        // Company media / numbered images
        "GetTenantCompanyImage"
    ];

    private static readonly string[] OwnerFacingExamples =
    [
        "How many vehicles are offline?",
        "What is our daily, weekly, and monthly revenue?",
        "Which vehicles have maintenance due?",
        "Who is the best performing driver today?",
        "How healthy is my fleet today?"
    ];

    private readonly IReadOnlyDictionary<string, IAiTool> _allowlisted = tools
        .Where(t => AllowlistedToolNames.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
        .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ListAllowlistedTools()
        => AllowlistedToolNames;

    public async Task<AiAssistantQueryResponse> QueryAsync(
        int tenantId,
        int userId,
        AiAssistantQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenantId));
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        var question = request.Question?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question is required.", nameof(request));

        var correlationId = executionAmbient.EnsureCorrelationId();
        logger.LogInformation(
            "AI assistant query start correlationId={CorrelationId} tenant={TenantId} user={UserId}",
            correlationId, tenantId, userId);

        var warnings = new List<string>();
        var (from, to, periodLabel) = AssistantPeriodHelper.Resolve(question, null);
        var vehicleId = request.VehicleId is int vid && vid > 0 ? vid : (int?)null;

        // Phase 7 — always persist assistant turns into SQL chat memory (tenant+user scoped).
        var ensured = await conversationMemory.EnsureSessionAsync(
            tenantId, userId, request.SessionId, title: null, question, cancellationToken);
        var sessionId = ensured.SessionId;
        await conversationMemory.AppendMessageAsync(sessionId, "user", question, cancellationToken);
        var memory = await conversationMemory.LoadAuthorizedAsync(
            tenantId, userId, sessionId, cancellationToken);

        var preAssembled = await contextBuilder.BuildAsync(
            new AiConversationContextRequest(
                tenantId, userId, question, memory,
                LiveToolResults: null,
                LiveMcpPrefetchSummary: null,
                VehicleId: vehicleId,
                IsPlatformSuperAdmin: request.IsPlatformSuperAdmin),
            cancellationToken);

        if (preAssembled.NeedsClarification && !string.IsNullOrWhiteSpace(preAssembled.ClarificationPrompt))
        {
            var clarify = preAssembled.ClarificationPrompt!;
            if (!string.IsNullOrWhiteSpace(ensured.Notice))
                clarify = ensured.Notice + "\n\n" + clarify;
            return await PersistAndReturnAsync(
                tenantId, userId, sessionId,
                new AiAssistantQueryResponse(
                    clarify, null, null, null, Array.Empty<string>(),
                    UsedLlm: false, Mode: "clarify", SessionId: sessionId),
                cancellationToken);
        }

        var isPlatformSuperAdmin = request.IsPlatformSuperAdmin;
        var tool = SelectTool(question, isPlatformSuperAdmin);
        if (tool is null)
        {
            // Same MCP+DeepInfra tool-calling loop as POST /api/ai/chat (FAB parity).
            var mcpLoop = await TryMcpToolCallingLoopAsync(
                tenantId, question, preAssembled, sessionId, cancellationToken);
            if (mcpLoop is not null)
                return await PersistAndReturnAsync(tenantId, userId, sessionId, mcpLoop with { SessionId = sessionId }, cancellationToken, ensured.Notice);

            // Deterministic Database MCP prefetch when keyword tools miss — grounded data only.
            var mcpFallback = await TryMcpRecordFallbackAsync(tenantId, question, cancellationToken);
            if (mcpFallback is not null)
                return await PersistAndReturnAsync(tenantId, userId, sessionId, mcpFallback with { SessionId = sessionId }, cancellationToken, ensured.Notice);

            var help = new StringBuilder();
            help.AppendLine("I am not sure what you need yet. Ask a clear question about your fleet, for example:");
            foreach (var example in OwnerFacingExamples)
                help.AppendLine($"• {example}");
            help.AppendLine("• List vehicles / drivers / bookings (Database MCP when bridge enabled)");

            return await PersistAndReturnAsync(
                tenantId, userId, sessionId,
                new AiAssistantQueryResponse(
                    help.ToString().Trim(),
                    Period: null,
                    null,
                    null,
                    Array.Empty<string>(),
                    UsedLlm: false,
                    Mode: "help",
                    SessionId: sessionId),
                cancellationToken,
                ensured.Notice);
        }

        // Only expose a date range for tools that actually filter by reporting period.
        AiAssistantPeriodDto? period = ToolUsesReportingPeriod(tool.Name)
            ? new AiAssistantPeriodDto(from, to)
            : null;

        var ctx = new AiToolExecutionContext(
            tenantId,
            userId,
            AllowWriteTools: false,
            ConfirmWrite: false,
            CanExecuteWrite: false,
            IsPlatformSuperAdmin: isPlatformSuperAdmin,
            VehicleId: vehicleId);

        var args = BuildArgsForTool(tool.Name, question, from, to);
        logger.LogInformation(
            "AI assistant query tenant={TenantId} user={UserId} tool={Tool} period={Period}",
            tenantId, userId, tool.Name, period is null ? "n/a" : periodLabel);

        AiToolExecutionResult result;
        try
        {
            result = await tool.ExecuteAsync(ctx, args, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI assistant tool {Tool} failed", tool.Name);
            return await PersistAndReturnAsync(
                tenantId, userId, sessionId,
                new AiAssistantQueryResponse(
                    "I could not retrieve that information right now. Please try again in a moment.",
                    period,
                    tool.Name,
                    null,
                    Array.Empty<string>(),
                    UsedLlm: false,
                    Mode: "error",
                    SessionId: sessionId),
                cancellationToken,
                ensured.Notice);
        }

        if (!result.Success)
        {
            // Image requests already resolved tenant/index — surface that error, don't invent MCP records.
            var skipMcpOnError = string.Equals(
                tool.Name, "GetTenantCompanyImage", StringComparison.OrdinalIgnoreCase);
            if (!skipMcpOnError)
            {
                var mcpLoop = await TryMcpToolCallingLoopAsync(
                    tenantId, question, preAssembled, sessionId, cancellationToken);
                if (mcpLoop is not null)
                    return await PersistAndReturnAsync(tenantId, userId, sessionId, mcpLoop with { SessionId = sessionId }, cancellationToken, ensured.Notice);

                var mcpFallback = await TryMcpRecordFallbackAsync(tenantId, question, cancellationToken);
                if (mcpFallback is not null)
                    return await PersistAndReturnAsync(tenantId, userId, sessionId, mcpFallback with { SessionId = sessionId }, cancellationToken, ensured.Notice);
            }

            return await PersistAndReturnAsync(
                tenantId, userId, sessionId,
                new AiAssistantQueryResponse(
                    result.Error ?? "I could not answer that right now. Please try again.",
                    period,
                    tool.Name,
                    null,
                    Array.Empty<string>(),
                    UsedLlm: false,
                    Mode: "tool_error",
                    SessionId: sessionId),
                cancellationToken,
                ensured.Notice);
        }

        var grounded = result.Summary;
        var usedLlm = false;
        var mode = "tools";

        // Re-assemble with live tool data separated from conversation memory.
        var assembled = await contextBuilder.BuildAsync(
            new AiConversationContextRequest(
                tenantId, userId, question, memory,
                LiveToolResults: [result],
                LiveMcpPrefetchSummary: null,
                VehicleId: vehicleId,
                IsPlatformSuperAdmin: isPlatformSuperAdmin),
            cancellationToken);

        // Keep image markdown + URL intact — LLM polish can strip displayable media.
        var skipLlmPolish = string.Equals(tool.Name, "GetTenantCompanyImage", StringComparison.OrdinalIgnoreCase);

        var (provider, config) = await providerResolver.ResolveAsync(tenantId, cancellationToken);
        if (!skipLlmPolish && provider is not null && config.IsEnabled && config.CopilotEnabled)
        {
            try
            {
                var modelName = string.IsNullOrWhiteSpace(config.ModelName)
                    ? (config.Provider is "DeepInfra" or "HostedLLM"
                        ? Providers.DeepInfraAiProvider.DefaultModel
                        : Providers.OllamaAiProvider.DefaultModel)
                    : config.ModelName!;
                var endpoint = string.IsNullOrWhiteSpace(config.ApiEndpoint)
                    ? (config.Provider is "DeepInfra" or "HostedLLM"
                        ? Providers.DeepInfraAiProvider.DefaultBaseUrl
                        : Providers.OllamaAiProvider.DefaultBaseUrl)
                    : config.ApiEndpoint;

                var system =
                    "You are SheikhGo AI Assistant for company owners and fleet managers. " +
                    "Rewrite the LIVE TOOL DATA into a short, plain-language answer to the user's question. " +
                    "Lead with the direct answer (counts, names, company). Use everyday words. " +
                    "Conversation context is for follow-ups only — never treat prior chat answers as current metrics. " +
                    "Do not invent numbers or vehicles. Do not list internal tool or report names. " +
                    "Do not dump menus of other questions unless the user asked what you can do. " +
                    "Never mention providers, models, LLM, SQL, or other tenants.\n\n" +
                    assembled.ToSystemPromptAppendix();

                var llmMessages = new List<AiChatMessageDto> { new("system", system) };
                llmMessages.AddRange(assembled.BoundedHistoryMessages);
                llmMessages.Add(new AiChatMessageDto(
                    "user",
                    $"Question: {question}\n\nLIVE TOOL DATA:\n{grounded}"));

                var llm = await provider.ChatAsync(
                    new AiProviderChatRequest(
                        modelName,
                        llmMessages,
                        endpoint,
                        Temperature: 0.2,
                        MaxTokens: 512),
                    cancellationToken);

                if (llm.Success && !string.IsNullOrWhiteSpace(llm.Content))
                {
                    grounded = llm.Content.Trim();
                    usedLlm = true;
                    mode = "tools+llm";
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "LLM polish skipped");
            }
        }

        // Soft usage ping (non-blocking for assistant flow)
        try
        {
            await aiManagement.RecordLearningAsync(
                tenantId, userId, "assistant_query", tool.Name, cancellationToken);
        }
        catch
        {
            // ignore learning failures
        }

        // Never surface internal diagnostics (provider/LLM/config) to the chat UI.
        _ = warnings;

        return await PersistAndReturnAsync(
            tenantId, userId, sessionId,
            new AiAssistantQueryResponse(
                grounded,
                period,
                tool.Name,
                result.Data,
                Array.Empty<string>(),
                usedLlm,
                mode,
                SessionId: sessionId),
            cancellationToken,
            ensured.Notice);
    }

    private async Task<AiAssistantQueryResponse> PersistAndReturnAsync(
        int tenantId,
        int userId,
        Guid sessionId,
        AiAssistantQueryResponse response,
        CancellationToken cancellationToken,
        string? tenantChangeNotice = null)
    {
        var answer = response.Answer;
        if (!string.IsNullOrWhiteSpace(tenantChangeNotice))
            answer = tenantChangeNotice + "\n\n" + answer;

        var final = response with
        {
            Answer = answer,
            SessionId = sessionId,
            CorrelationId = executionAmbient.CorrelationId ?? response.CorrelationId
        };
        try
        {
            string? payloadJson = null;
            if (final.Data is not null)
            {
                try
                {
                    payloadJson = JsonSerializer.Serialize(
                        final.Data,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    // Redact secrets without wrapping (must remain valid JSON for UI rehydrate).
                    payloadJson = Ai.Mcp.McpToolExecutionLogger.Sanitize(payloadJson) ?? payloadJson;
                    if (payloadJson.Length > 180_000)
                        payloadJson = null;
                }
                catch (Exception serEx)
                {
                    logger.LogDebug(serEx, "Assistant structured payload serialize skipped");
                    payloadJson = null;
                }
            }

            await conversationMemory.AppendMessageAsync(
                sessionId,
                "assistant",
                final.Answer,
                cancellationToken,
                structuredPayload: payloadJson,
                toolUsed: final.ToolUsed);
            await conversationMemory.TouchSessionAsync(sessionId, cancellationToken);
            await conversationMemory.MaybeSummarizeAsync(tenantId, userId, sessionId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Assistant conversation persist skipped");
        }

        logger.LogInformation(
            "AI assistant query end correlationId={CorrelationId} tenant={TenantId} mode={Mode} modelMs={ModelMs} mcpMs={McpMs}",
            final.CorrelationId, tenantId, final.Mode, executionAmbient.ModelLatencyMs, executionAmbient.McpLatencyMs);

        return final;
    }

    /// <summary>
    /// Shared chat MCP tool-calling loop (DeepInfra + allowlisted MCP). Soft-fails to null.
    /// </summary>
    private async Task<AiAssistantQueryResponse?> TryMcpToolCallingLoopAsync(
        int tenantId,
        string question,
        AiAssembledContext assembled,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!mcpToolLoop.IsAvailable)
            return null;

        try
        {
            var (provider, config) = await providerResolver.ResolveAsync(tenantId, cancellationToken);
            if (provider is null ||
                !config.IsEnabled ||
                !string.Equals(provider.ProviderName, "DeepInfra", StringComparison.OrdinalIgnoreCase))
                return null;

            var modelName = string.IsNullOrWhiteSpace(config.ModelName)
                ? Providers.DeepInfraAiProvider.DefaultModel
                : config.ModelName!;
            var baseUrl = string.IsNullOrWhiteSpace(config.ApiEndpoint)
                ? Providers.DeepInfraAiProvider.DefaultBaseUrl
                : config.ApiEndpoint!;

            var toolsUsed = new List<string>();
            var seed = new List<AiChatMessageDto>
            {
                new("system",
                    "Resolve follow-ups from conversation context; fetch live metrics via tools. " +
                    "Prior chat answers are NOT live data.\n\n" +
                    assembled.ToSystemPromptAppendix())
            };
            seed.AddRange(assembled.BoundedHistoryMessages);
            seed.Add(new AiChatMessageDto("user", question));

            var loopResult = await mcpToolLoop.RunAsync(
                provider, tenantId, modelName, baseUrl, seed, toolsUsed, cancellationToken,
                chatSessionId: sessionId);
            if (loopResult is null || string.IsNullOrWhiteSpace(loopResult.Answer))
                return null;

            logger.LogInformation(
                "AI assistant MCP tool-loop tenant={TenantId} mode={Mode} tools={Tools}",
                tenantId, loopResult.Mode, string.Join(",", toolsUsed));

            try
            {
                await aiManagement.RecordUsageAsync(
                    tenantId, "assistant", provider.ProviderName,
                    loopResult.Tokens, costUsd: 0, cancellationToken);
            }
            catch
            {
                // non-blocking
            }

            var mcpTool = toolsUsed.LastOrDefault(t => t.StartsWith("mcp:", StringComparison.OrdinalIgnoreCase))
                          ?? "mcp_tool_loop";

            // Prefer rich analytics JSON from MCP tools so the UI can render widgets.
            object data = loopResult.StructuredData
                          ?? new { source = "mcp_tool_loop", tools = toolsUsed };

            return new AiAssistantQueryResponse(
                loopResult.Answer,
                Period: null,
                ToolUsed: mcpTool,
                Data: data,
                Warnings: Array.Empty<string>(),
                UsedLlm: true,
                Mode: loopResult.Mode,
                SessionId: sessionId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP tool-calling loop failed for assistant query");
            return null;
        }
    }

    private async Task<AiAssistantQueryResponse?> TryMcpRecordFallbackAsync(
        int tenantId, string question, CancellationToken cancellationToken)
    {
        if (!mcpRecordRouter.LooksLikeRecordQuery(question))
            return null;

        try
        {
            var fetched = await mcpRecordRouter.TryFetchAsync(tenantId, question, cancellationToken);
            if (fetched is null || !fetched.Success || string.IsNullOrWhiteSpace(fetched.Summary))
                return null;

            logger.LogInformation(
                "AI assistant MCP DB fallback tenant={TenantId} tools={Tools}",
                tenantId, string.Join(",", fetched.ToolsUsed));

            return new AiAssistantQueryResponse(
                fetched.Summary,
                Period: null,
                ToolUsed: fetched.ToolsUsed.FirstOrDefault() ?? "mcp_database",
                Data: new { source = "database_mcp", tools = fetched.ToolsUsed },
                Warnings: Array.Empty<string>(),
                UsedLlm: false,
                Mode: "mcp_database",
                SessionId: null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP database record fallback failed");
            return null;
        }
    }

    private IAiTool? SelectTool(string question, bool isPlatformSuperAdmin)
    {
        var q = question.ToLowerInvariant();

        // Offline / GPS connectivity — highest priority for owner phrasing
        if (ContainsAny(q,
                "offline", "off line", "no gps", "not reporting", "disconnected",
                "gps offline", "vehicles offline", "vehicle offline", "how many offline",
                "which vehicles are offline", "which vehicle is offline", "not online"))
            return Get("GetOfflineVehicles");

        if (ContainsAny(q, "fleet health", "how healthy", "fleet overview", "fleet status", "gps online rate"))
            return Get("GetFleetHealth");

        // Subscription / entitlement modules (EN + HI + UR) — before trip/booking heuristics
        if (ContainsAny(q,
                "subscription module", "subscription modules", "subscribed modules",
                "enabled modules", "module entitlement", "which modules", "what modules",
                "modules are on", "modules enabled", "modules off", "tenant modules",
                "company modules", "licensed modules",
                "सब्सक्रिप्शन", "मॉड्यूल", "सब्सक्रिप्शन मॉड्यूल", "कौन से मॉड्यूल", "कौन सी सब्सक्रिप्शन",
                "سبسکرپشن", "ماڈیول", "modules on hain", "subscription modules konsi"))
            return Get("GetTenantSubscriptionModules");

        // Company / numbered image requests (EN + HI + UR)
        if (ContainsAny(q,
                "तस्वीर", "تصویر", "company image", "company photo", "company picture",
                "show image", "show photo", "show picture", "nth image", "9th image",
                "gallery image", "media image", "media library",
                "show me the", "show me image", "show me photo", "show me picture")
            && ContainsAny(q,
                "image", "photo", "picture", "तस्वीर", "تصویر", "media", "gallery"))
            return Get("GetTenantCompanyImage");
        if (ContainsAny(q, "तस्वीर", "تصویر")
            || (ContainsAny(q, "image", "photo", "picture")
                && ContainsAny(q, "shake", "sheikh", "शेक", "شیک", "travel", "company", "tenant", "ट्रैवल")))
            return Get("GetTenantCompanyImage");

        // Explicit priority for common operational phrasing (more specific first)
        if (ContainsAny(q, "tenant comparison", "compare tenants", "across tenants", "cross-tenant", "all tenants")
            && isPlatformSuperAdmin)
            return Get("GetTenantComparison");
        if (ContainsAny(q, "peak hour", "peak booking", "busiest hour", "busy hours", "dispatch peak", "booking hours"))
            return Get("GetPeakHoursAnalysis");
        if (ContainsAny(q, "sos", "panic", "emergency alert"))
            return Get("GetSosEventsSummary");
        if (ContainsAny(q, "idle time", "idling", "idle report", "most idle", "vehicle idle"))
            return Get("GetIdleTimeReport");
        if (ContainsAny(q, "expense", "expenses", "spending", "cost breakdown", "expenditure by"))
            return Get("GetExpenseTracking");
        if (ContainsAny(q, "top customer", "top customers", "best customers", "customers by revenue", "highest paying"))
            return Get("GetTopCustomersByRevenue");
        if (ContainsAny(q, "sales trend", "sales comparison", "compare sales", "revenue trend", "sales wow", "sales mom",
                "sales week over week", "sales month over month"))
            return Get("GetSalesTrendComparison");
        if (ContainsAny(q, "revenue", "sales total", "paid payment", "income total", "sales summary", "today's revenue", "todays revenue"))
            return Get("GetRevenueAnalytics");
        if (ContainsAny(q, "cost per km", "cost per kilometer", "cost/km", "per km"))
            return Get("GetCostPerKilometer");
        if (ContainsAny(q, "overspeed", "geofence", "speed exceeded", "geofence violation", "geofence breach"))
            return Get("GetOverspeedGeofenceSummary");
        if (ContainsAny(q, "fuel anomaly", "unusual fuel", "fuel investigation", "fuel outlier", "liters per 100", "litres per 100"))
            return Get("GetFuelAnomalyInvestigation");
        if (ContainsAny(q, "consumed most", "fuel usage", "fuel consumption", "most fuel", "liters used", "litres used"))
            return Get("GetFuelUsageTopVehicle");
        if (ContainsAny(q, "best driver", "best performing", "top driver", "driver performance", "top performing driver"))
            return Get("GetBestDriverPerformance");
        if (ContainsAny(q, "maintenance risk", "predictive maintenance", "oil change", "tire wear", "engine life", "may need maintenance"))
            return Get("GetMaintenanceRiskForecast");
        if (ContainsAny(q, "delay risk", "at risk of delay", "risk of delay", "late arrival",
                "trips at risk", "running late", "stale gps", "delayed trip"))
            return Get("GetTripDelayRisk");
        if (ContainsAny(q, "fuel", "litre", "liter", "petrol", "diesel", "expenditure"))
            return Get("GetFuelCostSummary");
        if (ContainsAny(q, "maintenance", "service due", "workshop", "overdue service", "maintenance alert"))
            return Get("GetVehicleMaintenanceSummary");
        if (ContainsAny(q, "invoice", "overdue bill", "unpaid", "receivable"))
            return Get("GetOverdueInvoices");
        if (ContainsAny(q, "utilization", "distance", "most used", "km driven"))
            return Get("GetVehicleUtilizationSummary");
        if (ContainsAny(q, "trip", "booking", "dispatch", "completed trip", "how many trip", "completion rate"))
            return Get("GetTripSummary");

        foreach (var tool in _allowlisted.Values)
        {
            if (tool.Name == "GetTenantComparison" && !isPlatformSuperAdmin)
                continue;
            if (tool.TriggerKeywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return tool;
        }

        return null;
    }

    private IAiTool? Get(string name)
        => _allowlisted.TryGetValue(name, out var t) ? t : null;

    private static bool ContainsAny(string q, params string[] tokens)
        => tokens.Any(t => q.Contains(t, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Tools that answer current operational state (not a calendar window) must not surface period meta.
    /// </summary>
    private static bool ToolUsesReportingPeriod(string toolName) => toolName switch
    {
        "GetVehicleMaintenanceSummary" => false,
        "GetOfflineVehicles" => false,
        "GetFleetHealth" => false,
        "GetOverdueInvoices" => false,
        "GetSosEventsSummary" => false,
        "GetMaintenanceRiskForecast" => false,
        "GetTripDelayRisk" => false,
        "GetTenantSubscriptionModules" => false,
        "GetTenantCompanyImage" => false,
        _ => true
    };

    private static JsonElement? BuildArgsForTool(
        string toolName, string question, DateTime from, DateTime toExclusive)
    {
        if (toolName is "GetTenantSubscriptionModules" or "GetTenantCompanyImage")
        {
            var hint = AssistantTenantNameNormalizer.ExtractTenantNameHint(question);
            var index = AssistantImageIndexParser.ExtractIndex(question, defaultIndex: 1);
            return JsonSerializer.SerializeToElement(new
            {
                tenantNameHint = hint,
                index = toolName == "GetTenantCompanyImage" ? index : (int?)null,
                question = toolName == "GetTenantCompanyImage" ? question : null
            });
        }

        if (!ToolUsesReportingPeriod(toolName))
            return null;

        return AssistantPeriodHelper.PeriodArgs(from, toExclusive);
    }
}
