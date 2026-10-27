using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Conversation;

/// <summary>
/// Phase 7 — RAG deferred: no manuals/policies/procedures corpus in Sheikh-Travel-System.
/// Live metrics continue to use SQL / MCP only.
/// </summary>
public sealed class DeferredAiRagRetriever : IAiRagRetriever
{
    public const string UnavailableReason =
        "No document corpus (manuals/policies/procedures) is configured in this repository. " +
        "RAG is deferred; use SQL/MCP tools for live fleet data.";

    public bool IsAvailable => false;

    public Task<AiRagRetrievalResult> TryRetrieveAsync(
        int tenantId,
        int userId,
        string query,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AiRagRetrievalResult(
            Available: false,
            Passages: [],
            UnavailableReason: UnavailableReason));
}
