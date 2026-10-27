namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Deterministic intent → allowlisted Database/Fleet/Bookings MCP tools when keyword <c>IAiTool</c>s are insufficient.
/// Does not replace ERP repositories for CRUD.
/// </summary>
public interface IMcpDatabaseRecordRouter
{
    /// <summary>True when the question looks like a live record / list / schema ask that MCP can answer.</summary>
    bool LooksLikeRecordQuery(string question);

    /// <summary>
    /// Resolve and call 1–2 allowlisted MCP tools for the question. Returns null when MCP is off or no intent matched.
    /// </summary>
    Task<McpRecordFetchResult?> TryFetchAsync(
        int tenantId,
        string question,
        CancellationToken cancellationToken = default);
}

/// <summary>One or more MCP tool results grounded for the assistant answer.</summary>
public sealed record McpRecordFetchResult(
    string Summary,
    IReadOnlyList<string> ToolsUsed,
    bool Success);
