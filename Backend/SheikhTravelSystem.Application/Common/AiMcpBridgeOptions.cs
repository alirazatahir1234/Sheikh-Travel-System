namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Opt-in SheikhGo MCP → SheikhGo AI Assistant bridge (ADR-AI-001: reporting/read-only only).
/// Bound from <c>Ai:McpBridge</c>; secrets and DB connection stay in env / user-secrets.
/// </summary>
public sealed class AiMcpBridgeOptions
{
    public const string SectionName = "Ai:McpBridge";

    /// <summary>Master switch. When false, chat uses existing IAiTool path only.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Process to spawn (e.g. <c>node</c> or absolute path).
    /// Overridden by env <c>SHEIKHGO_MCP_COMMAND</c> when set.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Args after command, e.g. <c>["/path/to/SheikhGo-MCP/dist/index.js"]</c>.
    /// Overridden by env <c>SHEIKHGO_MCP_ARGS</c> (JSON array or single path).
    /// </summary>
    public string[] Args { get; set; } = [];

    /// <summary>Optional working directory for the MCP process.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Absolute path to Sheikh-Travel-System monorepo for MCP file tools (passed as SHEIKHGO_REPOSITORY).
    /// Overridden by env <c>SHEIKHGO_REPOSITORY</c>.
    /// </summary>
    public string? RepositoryPath { get; set; }

    public int StartupTimeoutMs { get; set; } = 20_000;

    /// <summary>Per MCP tool-call timeout (linked to request cancellation).</summary>
    public int CallTimeoutMs { get; set; } = 45_000;

    /// <summary>Max LLM ↔ tool rounds per chat turn (safety cap, clamped 1–8).</summary>
    public int MaxToolRounds { get; set; } = 3;

    /// <summary>Max non-system seed messages kept from conversation history.</summary>
    public int MaxHistoryMessages { get; set; } = 20;

    /// <summary>
    /// Extra provider ChatAsync attempts after a transient failure (timeout / 429 / 5xx).
    /// Tool executions are never retried — only the LLM round is.
    /// </summary>
    public int ProviderMaxRetries { get; set; } = 2;

    /// <summary>Base delay (ms) for exponential backoff between provider retries.</summary>
    public int ProviderRetryBaseDelayMs { get; set; } = 400;
}
