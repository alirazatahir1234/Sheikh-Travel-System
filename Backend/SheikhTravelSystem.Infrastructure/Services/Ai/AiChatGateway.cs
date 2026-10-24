using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai.Providers;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

/// <summary>
/// Phase 1–3 AI Gateway: chat memory + Ollama/DeepInfra + tool engine + confirm-write execution.
/// Optional MCP bridge (Ai:McpBridge) adds allowlisted SheikhGo MCP tool-calling for Qwen/DeepInfra.
/// </summary>
public sealed class AiChatGateway(
    IDbConnectionFactory dbFactory,
    IAiProviderResolver providerResolver,
    IAiCopilotService ruleCopilot,
    IAiToolEngine toolEngine,
    IFleetHealthService fleetHealth,
    IAiManagementService aiManagement,
    ICurrentUserService currentUser,
    ISheikhGoMcpBridge mcpBridge,
    ILogger<AiChatGateway> logger) : IAiChatGateway
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(15);

    private bool CanExecuteWrite => currentUser.HasPermission(Application.Common.AiPermissions.ExecuteWrite);

    private static readonly string[] DefaultSuggestions =
    [
        "How healthy is my fleet today?",
        "Which vehicles are offline?",
        "Show critical GPS alerts",
        "What maintenance is overdue?",
        "Summarize driver risk this week"
    ];

    public async Task<AiChatTurnResponse> ChatAsync(
        int tenantId,
        int userId,
        AiChatTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        var isConfirm = request.ConfirmWrite || IsConfirmMessage(message);

        if (string.IsNullOrWhiteSpace(message) && !isConfirm)
            throw new ArgumentException("Message is required.");

        if (isConfirm && (request.SessionId is null || request.SessionId == Guid.Empty))
            throw new ArgumentException("SessionId is required to confirm a pending action.");

        var sessionId = isConfirm
            ? request.SessionId!.Value
            : await EnsureSessionAsync(tenantId, userId, request.SessionId, request.Title, message, cancellationToken);

        if (!isConfirm)
        {
            await AppendMessageAsync(sessionId, "user", message, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(message))
        {
            await AppendMessageAsync(sessionId, "user", message, cancellationToken);
        }

        logger.LogInformation(
            "AI chat turn tenant={TenantId} user={UserId} session={SessionId} confirm={Confirm}",
            tenantId, userId, sessionId, isConfirm);

        var (provider, config) = await providerResolver.ResolveAsync(tenantId, cancellationToken);
        string answer;
        var mode = "rules";
        var usedLlm = false;
        var tools = new List<string>();
        string? model = config.ModelName;
        AiPendingActionDto? pendingAction = null;

        if (isConfirm)
        {
            var executed = await ExecutePendingActionAsync(tenantId, userId, sessionId, cancellationToken);
            answer = executed.Answer;
            mode = executed.Mode;
            tools.AddRange(executed.ToolsUsed);
            logger.LogInformation("AI confirm executed session={SessionId} tools={Tools}", sessionId, string.Join(",", tools));
        }
        else
        {
            var history = await LoadHistoryAsync(sessionId, cancellationToken);
            var toolCtx = new AiToolExecutionContext(
                tenantId, userId, AllowWriteTools: true, ConfirmWrite: false, CanExecuteWrite: CanExecuteWrite);
            var toolResults = await toolEngine.SelectAndExecuteAsync(toolCtx, message, cancellationToken);
            foreach (var tr in toolResults.Where(tr => tr.Success && !tr.PendingConfirmation))
                tools.Add(tr.ToolName);

            logger.LogInformation(
                "AI tools selected session={SessionId} tools={Tools}",
                sessionId,
                string.Join(",", toolResults.Select(t => t.ToolName)));

            var pendingPreviews = toolResults.Where(t => t.PendingConfirmation).ToList();
            if (pendingPreviews.Count > 0)
            {
                var preview = pendingPreviews[0];
                var argsJson = preview.Data is not null
                    ? JsonSerializer.Serialize(preview.Data)
                    : "{}";
                pendingAction = await SavePendingActionAsync(
                    sessionId, tenantId, userId, preview.ToolName, argsJson, preview.Summary, cancellationToken);
                tools.Add("pending_confirm");
                tools.Add(preview.ToolName);
            }

            if (provider is not null && pendingPreviews.Count == 0)
            {
                var system = await BuildSystemPromptAsync(tenantId, toolResults, cancellationToken);
                var llmMessages = new List<AiChatMessageDto> { new("system", system) };
                llmMessages.AddRange(history.TakeLast(20));

                var modelName = ResolveModelName(config.Provider, config.ModelName);
                var baseUrl = ResolveProviderBase(config.Provider, config.ApiEndpoint);

                // Prefer MCP tool-calling loop when bridge is enabled (DeepInfra/Qwen).
                if (mcpBridge.IsEnabled &&
                    string.Equals(provider.ProviderName, "DeepInfra", StringComparison.OrdinalIgnoreCase))
                {
                    var mcpTurn = await RunMcpToolCallingLoopAsync(
                        provider, tenantId, modelName, baseUrl, llmMessages, tools, cancellationToken);
                    if (mcpTurn is not null)
                    {
                        answer = mcpTurn.Value.Answer;
                        mode = mcpTurn.Value.Mode;
                        usedLlm = true;
                        model = mcpTurn.Value.Model;
                        await aiManagement.RecordUsageAsync(
                            tenantId, "chat", provider.ProviderName,
                            mcpTurn.Value.Tokens, costUsd: 0, cancellationToken);
                    }
                    else
                    {
                        // Fall through to plain LLM polish with keyword tool data
                        var plain = await CallPlainLlmAsync(
                            provider, tenantId, userId, modelName, baseUrl, llmMessages,
                            toolResults, tools, cancellationToken);
                        answer = plain.Answer;
                        mode = plain.Mode;
                        usedLlm = plain.UsedLlm;
                        model = plain.Model ?? model;
                    }
                }
                else
                {
                    var plain = await CallPlainLlmAsync(
                        provider, tenantId, userId, modelName, baseUrl, llmMessages,
                        toolResults, tools, cancellationToken);
                    answer = plain.Answer;
                    mode = plain.Mode;
                    usedLlm = plain.UsedLlm;
                    model = plain.Model ?? model;
                }
            }
            else if (provider is not null && pendingPreviews.Count > 0)
            {
                answer = pendingPreviews[0].Summary;
                mode = "confirm_draft";
                if (toolResults.Any(t => t.Success && !t.PendingConfirmation))
                {
                    var context = string.Join("\n\n",
                        toolResults.Where(t => t.Success && !t.PendingConfirmation).Select(t => t.Summary));
                    answer = context + "\n\n" + answer;
                }
                answer += "\n\n_Click **Confirm** or reply **CONFIRM** to apply this change._";
            }
            else if (toolResults.Any(t => t.Success))
            {
                answer = string.Join("\n\n", toolResults.Where(t => t.Success).Select(t => t.Summary));
                mode = "tools_only";
            }
            else
            {
                var fallback = await ruleCopilot.AskAsync(tenantId, userId, message, cancellationToken);
                answer = fallback.Answer;
                mode = fallback.Mode;
                usedLlm = fallback.UsedLlm;
                tools.AddRange(fallback.ToolsUsed);
                if (config.Provider is "OpenAI" or "AzureOpenAI")
                {
                    answer += "\n\n_(OpenAI/Azure adapters are not used. Configure Provider = DeepInfra with model `Qwen/Qwen3-32B`, or Ollama with `qwen3:8b`.)_";
                }
            }
        }

        await AppendMessageAsync(sessionId, "assistant", answer, cancellationToken);
        await TouchSessionAsync(sessionId, cancellationToken);

        if (pendingAction is null && !isConfirm)
            pendingAction = await GetPendingActionAsync(tenantId, userId, sessionId, cancellationToken);

        return new AiChatTurnResponse(
            sessionId,
            answer,
            mode,
            usedLlm,
            config.Provider ?? "None",
            model,
            DefaultSuggestions,
            tools,
            pendingAction);
    }

    public async Task<AiPendingActionDto?> GetPendingActionAsync(
        int tenantId, int userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await PurgeExpiredPendingAsync(connection, cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<AiPendingActionDto>(new CommandDefinition("""
            SELECT p.ToolName, p.Summary, p.ExpiresAt
            FROM AiPendingActions p
            INNER JOIN AiChatSessions s ON s.Id = p.SessionId
            WHERE p.SessionId = @SessionId
              AND s.TenantId = @TenantId AND s.UserId = @UserId AND s.IsDeleted = 0
              AND p.ExpiresAt > GETUTCDATE()
            """, new { SessionId = sessionId, TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AiChatSessionDto>> ListSessionsAsync(
        int tenantId, int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<AiChatSessionDto>(new CommandDefinition("""
            SELECT TOP 50 s.Id, s.Title, s.CreatedAt, s.UpdatedAt,
                   (SELECT COUNT(*) FROM AiChatMessages m WHERE m.SessionId = s.Id) AS MessageCount
            FROM AiChatSessions s
            WHERE s.TenantId = @TenantId AND s.UserId = @UserId AND s.IsDeleted = 0
            ORDER BY s.UpdatedAt DESC
            """, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AiChatMessageDto>> GetMessagesAsync(
        int tenantId, int userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var owned = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM AiChatSessions
            WHERE Id = @Id AND TenantId = @TenantId AND UserId = @UserId AND IsDeleted = 0
            """, new { Id = sessionId, TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
        if (owned == 0) return [];

        var rows = await connection.QueryAsync<(string Role, string Content)>(new CommandDefinition("""
            SELECT Role, Content FROM AiChatMessages
            WHERE SessionId = @SessionId
            ORDER BY Id ASC
            """, new { SessionId = sessionId }, cancellationToken: cancellationToken));
        return rows.Select(r => new AiChatMessageDto(r.Role, r.Content)).ToList();
    }

    public async Task<AiProviderHealthDto> GetProviderHealthAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var (provider, config) = await providerResolver.ResolveAsync(tenantId, cancellationToken);
        var endpoint = ResolveProviderBase(config.Provider, config.ApiEndpoint);
        var model = ResolveModelName(config.Provider, config.ModelName);

        if (!config.IsEnabled || string.Equals(config.Provider, "None", StringComparison.OrdinalIgnoreCase))
        {
            return new AiProviderHealthDto("None", model, endpoint, false, false,
                "AI provider disabled — rule-based answers only.");
        }

        if (provider is null)
        {
            return new AiProviderHealthDto(config.Provider, model, endpoint, true, false,
                $"Provider '{config.Provider}' is not wired. Use DeepInfra (`Qwen/Qwen3-32B`) or Ollama (`qwen3:8b`).");
        }

        var reachable = await provider.IsAvailableAsync(endpoint, cancellationToken);
        var status = provider.ProviderName switch
        {
            "DeepInfra" when reachable =>
                $"DeepInfra reachable. Model hint: {model}. API key loaded from user-secrets/env.",
            "DeepInfra" =>
                "Cannot reach DeepInfra (or API key missing). Set DeepInfra:ApiKey / DEEPINFRA_API_KEY and check network.",
            "Ollama" when reachable =>
                $"Ollama reachable. Model hint: {model} (run `ollama pull {model}` if missing).",
            "Ollama" =>
                $"Cannot reach Ollama. Start with `ollama serve`, then `ollama pull {Providers.OllamaAiProvider.DefaultModel}`.",
            _ when reachable => $"{provider.ProviderName} reachable. Model hint: {model}.",
            _ => $"Cannot reach {provider.ProviderName}."
        };

        return new AiProviderHealthDto(
            provider.ProviderName,
            model,
            endpoint,
            true,
            reachable,
            status);
    }

    private async Task<(string Answer, string Mode, List<string> ToolsUsed)> ExecutePendingActionAsync(
        int tenantId, int userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var pending = await LoadPendingRowAsync(tenantId, userId, sessionId, cancellationToken);
        if (pending is null)
        {
            return ("No pending action found for this session (it may have expired). Please repeat your request.",
                "confirm_failed", ["confirm_failed"]);
        }

        JsonElement? args = null;
        if (!string.IsNullOrWhiteSpace(pending.ArgsJson))
        {
            using var doc = JsonDocument.Parse(pending.ArgsJson);
            args = doc.RootElement.Clone();
        }

        var ctx = new AiToolExecutionContext(
            tenantId, userId, AllowWriteTools: true, ConfirmWrite: true, CanExecuteWrite: CanExecuteWrite);
        var result = await toolEngine.ExecuteAsync(pending.ToolName, ctx, args, cancellationToken);
        await ClearPendingActionAsync(sessionId, cancellationToken);

        if (!result.Success)
        {
            return ($"Could not apply **{pending.ToolName}**: {result.Error ?? "Unknown error"}",
                "confirm_failed", [pending.ToolName, "confirm_failed"]);
        }

        return ($"✓ {result.Summary}", "confirm_executed", [pending.ToolName, "confirm_executed"]);
    }

    private async Task<AiPendingActionDto> SavePendingActionAsync(
        Guid sessionId, int tenantId, int userId,
        string toolName, string argsJson, string summary,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var id = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.Add(PendingTtl);

        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM AiPendingActions WHERE SessionId = @SessionId;

            INSERT INTO AiPendingActions (Id, SessionId, TenantId, UserId, ToolName, ArgsJson, Summary, CreatedAt, ExpiresAt)
            VALUES (@Id, @SessionId, @TenantId, @UserId, @ToolName, @ArgsJson, @Summary, GETUTCDATE(), @ExpiresAt);
            """,
            new { Id = id, SessionId = sessionId, TenantId = tenantId, UserId = userId, ToolName = toolName, ArgsJson = argsJson, Summary = summary, ExpiresAt = expiresAt },
            cancellationToken: cancellationToken));

        return new AiPendingActionDto(toolName, summary, expiresAt);
    }

    private sealed record PendingRow(string ToolName, string ArgsJson, string Summary);

    private async Task<PendingRow?> LoadPendingRowAsync(
        int tenantId, int userId, Guid sessionId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await PurgeExpiredPendingAsync(connection, cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<PendingRow>(
            new CommandDefinition("""
                SELECT p.ToolName, p.ArgsJson, p.Summary
                FROM AiPendingActions p
                INNER JOIN AiChatSessions s ON s.Id = p.SessionId
                WHERE p.SessionId = @SessionId
                  AND s.TenantId = @TenantId AND s.UserId = @UserId AND s.IsDeleted = 0
                  AND p.ExpiresAt > GETUTCDATE()
                """, new { SessionId = sessionId, TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    private async Task ClearPendingActionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AiPendingActions WHERE SessionId = @SessionId",
            new { SessionId = sessionId }, cancellationToken: cancellationToken));
    }

    private static async Task PurgeExpiredPendingAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AiPendingActions WHERE ExpiresAt <= GETUTCDATE()", cancellationToken: ct));
    }


    private static bool IsConfirmMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return Regex.IsMatch(message.Trim(), @"^CONFIRM(\s|$)", RegexOptions.IgnoreCase);
    }

    private async Task<string> BuildSystemPromptAsync(
        int tenantId,
        IReadOnlyList<AiToolExecutionResult> toolResults,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are SheikhGo AI Assistant for company owners and fleet managers.");
        sb.AppendLine("Answer the user's question directly in plain language. Lead with the answer (counts, names, company).");
        sb.AppendLine("Base answers strictly on the tool data provided below. Do not invent vehicle IDs, driver names, plate numbers, or GPS coordinates.");
        sb.AppendLine("Do not dump menus of reports or tool names. Do not mention providers, models, or LLM.");
        sb.AppendLine("If data is missing, say so briefly. Stay on topic: fleet, GPS, bookings, maintenance, drivers, and compliance.");

        var succeeded = toolResults.Where(t => t.Success && !t.PendingConfirmation).ToList();
        if (succeeded.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== Live fleet data (from tools — treat as ground truth) ===");
            foreach (var tr in succeeded)
            {
                sb.AppendLine($"[{tr.ToolName}]");
                sb.AppendLine(tr.Summary);
            }
            sb.AppendLine("=== End of live data ===");
        }
        else
        {
            try
            {
                var health = await fleetHealth.ComputeAsync(tenantId, ct);
                sb.AppendLine();
                sb.AppendLine("Fleet snapshot:");
                sb.AppendLine($"- Health: {health.HealthPercent:0.#}%, GPS online: {health.GpsOnlineRate:0.#}%");
                sb.AppendLine($"- Maintenance: {health.MaintenanceScore:0.#}%, compliance: {health.ComplianceScore:0.#}%");
                sb.AppendLine($"- Driver score: {health.DriverScore:0.#}, critical alerts: {health.CriticalAlerts}");
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not inject fleet health into system prompt");
            }
        }

        return sb.ToString();
    }

    private async Task<Guid> EnsureSessionAsync(
        int tenantId, int userId, Guid? sessionId, string? title, string firstMessage, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        if (sessionId is Guid id && id != Guid.Empty)
        {
            var ok = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM AiChatSessions
                WHERE Id = @Id AND TenantId = @TenantId AND UserId = @UserId AND IsDeleted = 0
                """, new { Id = id, TenantId = tenantId, UserId = userId }, cancellationToken: ct));
            if (ok > 0) return id;
        }

        var newId = Guid.NewGuid();
        var sessionTitle = string.IsNullOrWhiteSpace(title)
            ? (firstMessage.Length > 60 ? firstMessage[..60] + "…" : firstMessage)
            : title!.Trim();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AiChatSessions (Id, TenantId, UserId, Title, CreatedAt, UpdatedAt, IsDeleted)
            VALUES (@Id, @TenantId, @UserId, @Title, GETUTCDATE(), GETUTCDATE(), 0)
            """,
            new { Id = newId, TenantId = tenantId, UserId = userId, Title = sessionTitle },
            cancellationToken: ct));
        return newId;
    }

    private async Task AppendMessageAsync(Guid sessionId, string role, string content, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AiChatMessages (SessionId, Role, Content, CreatedAt)
            VALUES (@SessionId, @Role, @Content, GETUTCDATE())
            """, new { SessionId = sessionId, Role = role, Content = content }, cancellationToken: ct));
    }

    private async Task TouchSessionAsync(Guid sessionId, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE AiChatSessions SET UpdatedAt = GETUTCDATE() WHERE Id = @Id
            """, new { Id = sessionId }, cancellationToken: ct));
    }

    private async Task<List<AiChatMessageDto>> LoadHistoryAsync(Guid sessionId, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<(string Role, string Content)>(new CommandDefinition("""
            SELECT Role, Content FROM AiChatMessages
            WHERE SessionId = @SessionId
            ORDER BY Id ASC
            """, new { SessionId = sessionId }, cancellationToken: ct));
        return rows.Select(r => new AiChatMessageDto(r.Role, r.Content)).ToList();
    }

    private async Task<(string Answer, string Mode, bool UsedLlm, string? Model)> CallPlainLlmAsync(
        IAiProvider provider,
        int tenantId,
        int userId,
        string modelName,
        string baseUrl,
        List<AiChatMessageDto> llmMessages,
        IReadOnlyList<AiToolExecutionResult> toolResults,
        List<string> tools,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("AI calling LLM provider={Provider} model={Model}", provider.ProviderName, modelName);
        var result = await provider.ChatAsync(
            new AiProviderChatRequest(modelName, llmMessages, baseUrl),
            cancellationToken);

        if (result.Success)
        {
            tools.Add("llm_chat");
            await aiManagement.RecordUsageAsync(
                tenantId, "chat", result.Provider,
                result.PromptTokens + result.CompletionTokens,
                costUsd: 0,
                cancellationToken);
            return (result.Content, "llm", true, result.Model);
        }

        logger.LogInformation("LLM unavailable ({Error}); falling back to tool summaries", result.Error);
        if (toolResults.Any(t => t.Success))
        {
            tools.Add("llm_fallback");
            return (
                string.Join("\n\n", toolResults.Where(t => t.Success).Select(t => t.Summary))
                + $"\n\n_(LLM offline: {result.Error})_",
                "tools_only",
                false,
                null);
        }

        var fallback = await ruleCopilot.AskAsync(tenantId, userId, llmMessages.LastOrDefault(m => m.Role == "user")?.Content ?? "", cancellationToken);
        tools.AddRange(fallback.ToolsUsed);
        tools.Add("llm_fallback");
        return ($"{fallback.Answer}\n\n_(LLM note: {result.Error})_", "rules_fallback", false, null);
    }

    /// <summary>
    /// DeepInfra/Qwen tool-calling loop against allowlisted MCP tools. Returns null to fall back.
    /// </summary>
    private async Task<(string Answer, string Mode, string? Model, int Tokens)?> RunMcpToolCallingLoopAsync(
        IAiProvider provider,
        int tenantId,
        string modelName,
        string baseUrl,
        List<AiChatMessageDto> seedMessages,
        List<string> toolsUsed,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AiLlmToolDefinition> mcpTools;
        try
        {
            mcpTools = await mcpBridge.GetAllowlistedLlmToolsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP schemas unavailable; skipping tool-calling loop");
            return null;
        }

        if (mcpTools.Count == 0)
        {
            logger.LogInformation("MCP bridge enabled but no allowlisted schemas; skipping loop");
            return null;
        }

        // Instruct model to use tools for live ops data; never invent tenant scope
        var mcpSystem = new AiChatMessageDto(
            "system",
            "You are SheikhGo AI Assistant for fleet operators. " +
            "Use the provided tools for live operational data (trips, fuel, GPS, alerts, bookings). " +
            "Do not invent numbers. Tenant scope is applied by the server — never ask for or pass tenantId. " +
            "Do not attempt Architecture, code, scaffold, or write tools. " +
            "After tools return, answer concisely in the user's language.");

        var messages = new List<AiChatMessageDto> { mcpSystem };
        // Keep prior system (keyword tool data) as extra context, then history
        messages.AddRange(seedMessages);

        var totalTokens = 0;
        string? lastModel = modelName;
        const int maxRounds = 3;

        for (var round = 0; round < maxRounds; round++)
        {
            logger.LogInformation(
                "AI MCP tool-calling round={Round} provider={Provider} model={Model} tools={ToolCount}",
                round + 1, provider.ProviderName, modelName, mcpTools.Count);

            var result = await provider.ChatAsync(
                new AiProviderChatRequest(
                    modelName,
                    messages,
                    baseUrl,
                    Temperature: 0.2,
                    MaxTokens: 1024,
                    Tools: mcpTools,
                    ToolChoice: "auto"),
                cancellationToken);

            totalTokens += result.PromptTokens + result.CompletionTokens;
            lastModel = result.Model;

            if (!result.Success)
            {
                logger.LogInformation("MCP LLM round failed: {Error}", result.Error);
                return null;
            }

            toolsUsed.Add("llm_chat");
            toolsUsed.Add("mcp_bridge");

            if (result.ToolCalls is not { Count: > 0 })
            {
                if (string.IsNullOrWhiteSpace(result.Content))
                    return null;

                toolsUsed.Add("mcp_tool_loop");
                return (result.Content.Trim(), "mcp+llm", lastModel, totalTokens);
            }

            // Append assistant tool_calls message, then execute each allowlisted tool
            messages.Add(new AiChatMessageDto(
                "assistant",
                result.Content ?? string.Empty,
                ToolCalls: result.ToolCalls));

            foreach (var call in result.ToolCalls)
            {
                toolsUsed.Add($"mcp:{call.Name}");
                var args = ParseToolArguments(call.ArgumentsJson);
                McpToolCallResult mcpResult;
                try
                {
                    mcpResult = await mcpBridge.CallAsync(tenantId, call.Name, args, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "MCP execute failed for {Tool}", call.Name);
                    mcpResult = new McpToolCallResult(false, call.Name, string.Empty, ex.Message);
                }

                var payload = mcpResult.Success
                    ? mcpResult.Content
                    : $"ERROR: {mcpResult.Error ?? "tool failed"}";

                messages.Add(new AiChatMessageDto(
                    "tool",
                    TruncateForLlm(payload, 8000),
                    ToolCallId: call.Id));
            }
        }

        // Final wording pass without tools if we exhausted rounds with pending tool data
        var final = await provider.ChatAsync(
            new AiProviderChatRequest(modelName, messages, baseUrl, Temperature: 0.2, MaxTokens: 768),
            cancellationToken);
        totalTokens += final.PromptTokens + final.CompletionTokens;
        if (!final.Success || string.IsNullOrWhiteSpace(final.Content))
            return null;

        toolsUsed.Add("mcp_tool_loop");
        return (final.Content.Trim(), "mcp+llm", final.Model, totalTokens);
    }

    private static Dictionary<string, object?> ParseToolArguments(string? argumentsJson)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return dict;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return dict;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number when prop.Value.TryGetInt64(out var l) => l,
                    JsonValueKind.Number when prop.Value.TryGetDouble(out var d) => d,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }
        catch (JsonException)
        {
            // leave empty — bridge still injects tenantId
        }

        return dict;
    }

    private static string TruncateForLlm(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    private static string ResolveProviderBase(string? provider, string? endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
            return endpoint.Trim().TrimEnd('/');

        return provider?.Trim() switch
        {
            "DeepInfra" or "HostedLLM" => Providers.DeepInfraAiProvider.DefaultBaseUrl,
            _ => Providers.OllamaAiProvider.DefaultBaseUrl
        };
    }

    private static string ResolveModelName(string? provider, string? modelName)
    {
        if (!string.IsNullOrWhiteSpace(modelName))
            return modelName.Trim();

        return provider?.Trim() switch
        {
            "DeepInfra" or "HostedLLM" => Providers.DeepInfraAiProvider.DefaultModel,
            _ => Providers.OllamaAiProvider.DefaultModel
        };
    }
}
