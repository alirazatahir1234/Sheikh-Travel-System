using System.Text;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

/// <summary>
/// Phase 2 AI Assistant orchestrator: allowlisted read-only tools only.
/// LLM (if configured) may polish wording; it never supplies tenant ID or invents SQL.
/// </summary>
public sealed class AiAssistantGateway(
    IEnumerable<IAiTool> tools,
    IAiProviderResolver providerResolver,
    IAiManagementService aiManagement,
    ILogger<AiAssistantGateway> logger) : IAiAssistantGateway
{
    public static readonly string[] AllowlistedToolNames =
    [
        "GetFuelCostSummary",
        "GetVehicleMaintenanceSummary",
        "GetOverdueInvoices",
        "GetTripSummary",
        "GetVehicleUtilizationSummary",
        // Batch 1
        "GetRevenueAnalytics",
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
        "GetFuelAnomalyInvestigation"
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

        var warnings = new List<string>();
        var (from, to, periodLabel) = AssistantPeriodHelper.Resolve(question, null);
        var period = new AiAssistantPeriodDto(from, to);

        var isPlatformSuperAdmin = request.IsPlatformSuperAdmin;
        var tool = SelectTool(question, isPlatformSuperAdmin);
        if (tool is null)
        {
            warnings.Add("No matching reporting tool; listing capabilities.");
            var help = new StringBuilder();
            help.AppendLine("I can answer operational questions using these reports:");
            foreach (var name in AllowlistedToolNames)
            {
                if (name == "GetTenantComparison" && !isPlatformSuperAdmin)
                    continue;
                help.AppendLine($"• {name}");
            }
            help.AppendLine();
            help.AppendLine(
                "Try: “What is our daily, weekly, and monthly revenue?”, “Which vehicles are at maintenance risk?”, " +
                "or “Which trips are at risk of delay?”");

            return new AiAssistantQueryResponse(
                help.ToString().Trim(),
                period,
                null,
                null,
                warnings,
                UsedLlm: false,
                Mode: "help");
        }

        var ctx = new AiToolExecutionContext(
            tenantId,
            userId,
            AllowWriteTools: false,
            ConfirmWrite: false,
            CanExecuteWrite: false,
            IsPlatformSuperAdmin: isPlatformSuperAdmin);

        var args = AssistantPeriodHelper.PeriodArgs(from, to);
        logger.LogInformation(
            "AI assistant query tenant={TenantId} user={UserId} tool={Tool} period={Period}",
            tenantId, userId, tool.Name, periodLabel);

        AiToolExecutionResult result;
        try
        {
            result = await tool.ExecuteAsync(ctx, args, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI assistant tool {Tool} failed", tool.Name);
            warnings.Add("Reporting query failed; see logs for details.");
            return new AiAssistantQueryResponse(
                "I could not retrieve that report right now. Please try again or open the standard reports page.",
                period,
                tool.Name,
                null,
                warnings,
                UsedLlm: false,
                Mode: "error");
        }

        if (!result.Success)
        {
            warnings.Add(result.Error ?? "Tool returned an error.");
            return new AiAssistantQueryResponse(
                result.Error ?? "Unable to run that report.",
                period,
                tool.Name,
                null,
                warnings,
                UsedLlm: false,
                Mode: "tool_error");
        }

        var grounded = result.Summary;
        var usedLlm = false;
        var mode = "tools";

        var (provider, config) = await providerResolver.ResolveAsync(tenantId, cancellationToken);
        if (provider is not null && config.IsEnabled && config.CopilotEnabled)
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
                var llm = await provider.ChatAsync(
                    new AiProviderChatRequest(
                        modelName,
                        [
                            new AiChatMessageDto(
                                "system",
                                "You are SheikhGo AI Assistant. Answer ONLY from the TOOL DATA below. " +
                                "Do not invent numbers, vehicles, or invoices. If data is empty, say so. " +
                                "Keep the answer concise. Never mention other tenants."),
                            new AiChatMessageDto(
                                "user",
                                $"Question: {question}\n\nTOOL ({tool.Name}) DATA:\n{grounded}")
                        ],
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
                else
                {
                    warnings.Add("LLM unavailable; returning grounded tool summary.");
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "LLM polish skipped");
                warnings.Add("LLM unavailable; returning grounded tool summary.");
            }
        }
        else
        {
            warnings.Add("LLM not configured — returning grounded tool summary. Configure AI provider for natural-language polish.");
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

        return new AiAssistantQueryResponse(
            grounded,
            period,
            tool.Name,
            result.Data,
            warnings,
            usedLlm,
            mode);
    }

    private IAiTool? SelectTool(string question, bool isPlatformSuperAdmin)
    {
        var q = question.ToLowerInvariant();

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
        if (ContainsAny(q, "revenue", "sales total", "paid payment", "income total"))
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
}
