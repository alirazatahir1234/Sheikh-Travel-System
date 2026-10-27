namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Phase 6 — persist structured MCP tool execution rows (sanitized; no secrets).
/// Best-effort / non-blocking for the tool-calling path.
/// </summary>
public interface IMcpToolExecutionLogger
{
    Task LogAsync(McpToolExecutionLogEntry entry, CancellationToken cancellationToken = default);
}

/// <param name="TenantId">Tenant scope (required).</param>
/// <param name="ToolName">Allowlisted MCP tool name.</param>
/// <param name="Success">Whether the MCP call returned success.</param>
/// <param name="DurationMs">Wall-clock duration of the call.</param>
/// <param name="CorrelationId">Per-call correlation id (bridge-generated when absent).</param>
/// <param name="SessionId">AiChatSessions Id when chat-scoped; null for FAB / router.</param>
/// <param name="UserId">Caller user id when known.</param>
/// <param name="ArgsSanitized">Redacted/truncated argument preview (never raw secrets).</param>
/// <param name="ErrorSanitized">Redacted/truncated error when failed.</param>
public sealed record McpToolExecutionLogEntry(
    int TenantId,
    string ToolName,
    bool Success,
    int DurationMs,
    string? CorrelationId,
    Guid? SessionId,
    int? UserId,
    string? ArgsSanitized,
    string? ErrorSanitized);
