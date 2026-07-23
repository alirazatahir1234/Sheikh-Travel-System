using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai.Providers;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

/// <summary>
/// Phase 1–3 AI Gateway: chat memory + Ollama/Mistral + tool engine + confirm-write execution.
/// </summary>
public sealed class AiChatGateway(
    IDbConnectionFactory dbFactory,
    IAiProviderResolver providerResolver,
    IAiCopilotService ruleCopilot,
    IAiToolEngine toolEngine,
    IFleetHealthService fleetHealth,
    IAiManagementService aiManagement,
    ILogger<AiChatGateway> logger) : IAiChatGateway
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(15);

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
            var toolCtx = new AiToolExecutionContext(tenantId, userId, AllowWriteTools: true, ConfirmWrite: false);
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

                logger.LogInformation("AI calling LLM provider={Provider} model={Model}", provider.ProviderName, config.ModelName);
                var result = await provider.ChatAsync(
                    new AiProviderChatRequest(
                        string.IsNullOrWhiteSpace(config.ModelName) ? "mistral" : config.ModelName!,
                        llmMessages,
                        ResolveOllamaBase(config.ApiEndpoint)),
                    cancellationToken);

                if (result.Success)
                {
                    answer = result.Content;
                    mode = "llm";
                    usedLlm = true;
                    model = result.Model;
                    tools.Add("llm_chat");
                    await aiManagement.RecordUsageAsync(
                        tenantId, "chat", result.Provider,
                        result.PromptTokens + result.CompletionTokens,
                        costUsd: 0,
                        cancellationToken);
                }
                else
                {
                    logger.LogInformation("LLM unavailable ({Error}); falling back to tool summaries", result.Error);
                    if (toolResults.Any(t => t.Success))
                    {
                        answer = string.Join("\n\n", toolResults.Where(t => t.Success).Select(t => t.Summary))
                                 + $"\n\n_(LLM offline: {result.Error})_";
                        mode = "tools_only";
                        tools.Add("llm_fallback");
                    }
                    else
                    {
                        var fallback = await ruleCopilot.AskAsync(tenantId, userId, message, cancellationToken);
                        answer = $"{fallback.Answer}\n\n_(LLM note: {result.Error})_";
                        mode = "rules_fallback";
                        tools.AddRange(fallback.ToolsUsed);
                        tools.Add("llm_fallback");
                    }
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
                    answer += "\n\n_(Cloud LLM adapters ship in Phase 2. For Phase 1 use Provider = Ollama with model `mistral`.)_";
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
        var endpoint = string.IsNullOrWhiteSpace(config.ApiEndpoint)
            ? "http://127.0.0.1:11434"
            : config.ApiEndpoint!.Trim().TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(config.ModelName) ? "mistral" : config.ModelName;

        if (!config.IsEnabled || string.Equals(config.Provider, "None", StringComparison.OrdinalIgnoreCase))
        {
            return new AiProviderHealthDto("None", model, endpoint, false, false,
                "AI provider disabled — rule-based answers only.");
        }

        if (provider is null)
        {
            return new AiProviderHealthDto(config.Provider, model, endpoint, true, false,
                $"Provider '{config.Provider}' is configured but not available in Phase 1. Use Ollama + Mistral.");
        }

        var reachable = await provider.IsAvailableAsync(endpoint, cancellationToken);
        return new AiProviderHealthDto(
            provider.ProviderName,
            model,
            endpoint,
            true,
            reachable,
            reachable
                ? $"Ollama reachable. Model hint: {model} (run `ollama pull {model}` if missing)."
                : "Cannot reach Ollama. Start with `ollama serve`, then `ollama pull mistral`.");
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

        var ctx = new AiToolExecutionContext(tenantId, userId, AllowWriteTools: true, ConfirmWrite: true);
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
        sb.AppendLine("You are SheikhGo AI, a helpful fleet management assistant for SheikhGo ERP.");
        sb.AppendLine("Answer clearly and concisely in the same language as the user's question.");
        sb.AppendLine("Base answers strictly on the tool data provided below. Do not invent vehicle IDs, driver names, plate numbers, or GPS coordinates.");
        sb.AppendLine("If data is missing or unknown, say so and suggest which ERP screen to check.");
        sb.AppendLine("Stay on topic: fleet, GPS, bookings, maintenance, drivers, compliance, and reports.");

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

    private static string ResolveOllamaBase(string? endpoint)
        => string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:11434" : endpoint.Trim().TrimEnd('/');
}
