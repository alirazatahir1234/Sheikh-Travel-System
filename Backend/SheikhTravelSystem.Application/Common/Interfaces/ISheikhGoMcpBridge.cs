namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Glue layer: SheikhGo AI Assistant chat → allowlisted SheikhGo MCP tools (stdio) with server-side tenant injection.
/// Architecture / write / scaffold tools are never exposed (ADR-AI-001).
/// </summary>
public interface ISheikhGoMcpBridge
{
    bool IsEnabled { get; }

    IReadOnlyList<string> ListAllowlistedToolNames();

    /// <summary>OpenAI-style function schemas for allowlisted tools (for DeepInfra/Qwen tool-calling).</summary>
    Task<IReadOnlyList<AiLlmToolDefinition>> GetAllowlistedLlmToolsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute one allowlisted MCP tool. Always injects <paramref name="tenantId"/> from auth context;
    /// client-supplied tenantId in arguments is overwritten.
    /// </summary>
    Task<McpToolCallResult> CallAsync(
        int tenantId,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default);
}

public record AiLlmToolDefinition(
    string Name,
    string Description,
    object ParametersSchema);

public record AiToolCallDto(
    string Id,
    string Name,
    string ArgumentsJson);

public record McpToolCallResult(
    bool Success,
    string ToolName,
    string Content,
    string? Error = null);
