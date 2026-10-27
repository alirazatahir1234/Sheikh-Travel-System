namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Scoped ambient context for one AI turn (chat / assistant).
/// Set early by gateways; consumed by the MCP loop, bridge, and controller correlation header.
/// </summary>
public interface IMcpExecutionAmbient
{
    Guid? ChatSessionId { get; set; }

    /// <summary>Turn-scoped correlation id (32-char hex). Shared across model rounds and MCP tool calls.</summary>
    string? CorrelationId { get; set; }

    /// <summary>1-based count of MCP tool invocations in this turn (for structured logs).</summary>
    int ToolCallIndex { get; set; }

    /// <summary>Cumulative DeepInfra / provider chat latency for the turn (ms).</summary>
    long ModelLatencyMs { get; set; }

    /// <summary>Cumulative MCP tool latency for the turn (ms).</summary>
    long McpLatencyMs { get; set; }

    /// <summary>Ensures a turn correlation id exists; does not replace an existing one.</summary>
    string EnsureCorrelationId();

    void Clear();
}
