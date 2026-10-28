namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Shared DeepInfra ↔ allowlisted MCP tool-calling loop used by chat and FAB assistant.
/// Tenant is always injected by the MCP bridge — never from the model.
/// </summary>
public interface IAiMcpToolCallingLoop
{
    /// <summary>True when <c>Ai:McpBridge:Enabled</c> and Command/Args (or env) are configured.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Runs the OpenAI-style tools loop.
    /// Returns null only when the bridge/schemas are unavailable (caller may fall through).
    /// On provider/tool failure after the loop starts, returns a safe user-facing answer
    /// (<see cref="AiMcpToolLoopResult.Mode"/> = <c>mcp+llm_error</c>) — never fabricated business data.
    /// </summary>
    Task<AiMcpToolLoopResult?> RunAsync(
        IAiProvider provider,
        int tenantId,
        string modelName,
        string baseUrl,
        IReadOnlyList<AiChatMessageDto> seedMessages,
        IList<string> toolsUsed,
        CancellationToken cancellationToken = default,
        Guid? chatSessionId = null);
}

/// <param name="Answer">Final text for Angular (never includes secrets / exception internals).</param>
/// <param name="Mode"><c>mcp+llm</c> on success; <c>mcp+llm_error</c> for safe failure UX.</param>
/// <param name="Model">Provider model id when known.</param>
/// <param name="Tokens">Cumulative prompt + completion tokens for the turn.</param>
/// <param name="HadToolFailures">True when at least one tool call failed / was rejected.</param>
/// <param name="SuccessfulToolCount">Count of MCP executions that returned Success.</param>
/// <param name="StructuredData">
/// Last analytics-shaped MCP JSON (responseType / kpis) for rich UI widgets — not sent to the LLM again.
/// </param>
public record AiMcpToolLoopResult(
    string Answer,
    string Mode,
    string? Model,
    int Tokens,
    bool HadToolFailures = false,
    int SuccessfulToolCount = 0,
    object? StructuredData = null);
