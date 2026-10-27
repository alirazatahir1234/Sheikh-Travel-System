namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Phase 7 — assembles three distinct context layers for the LLM / tool loop.
/// Conversation ≠ business identity ≠ live retrieved data.
/// </summary>
public interface IAiConversationContextBuilder
{
    Task<AiAssembledContext> BuildAsync(
        AiConversationContextRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AiConversationContextRequest(
    int TenantId,
    int UserId,
    string CurrentUserMessage,
    AiConversationMemorySnapshot? Memory,
    IReadOnlyList<AiToolExecutionResult>? LiveToolResults = null,
    string? LiveMcpPrefetchSummary = null,
    int? VehicleId = null,
    bool IsPlatformSuperAdmin = false);

/// <summary>Three separated context types + follow-up resolution metadata.</summary>
public sealed record AiAssembledContext(
    string ConversationSection,
    string BusinessSection,
    string LiveDataSection,
    string? RagSection,
    bool LooksLikeFollowUp,
    bool NeedsClarification,
    string? ClarificationPrompt,
    IReadOnlyList<AiChatMessageDto> BoundedHistoryMessages)
{
    /// <summary>System prompt block combining the three layers (RAG optional / deferred).</summary>
    public string ToSystemPromptAppendix()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(BusinessSection))
            parts.Add(BusinessSection.Trim());
        if (!string.IsNullOrWhiteSpace(ConversationSection))
            parts.Add(ConversationSection.Trim());
        if (!string.IsNullOrWhiteSpace(LiveDataSection))
            parts.Add(LiveDataSection.Trim());
        if (!string.IsNullOrWhiteSpace(RagSection))
            parts.Add(RagSection.Trim());
        return string.Join("\n\n", parts);
    }
}

/// <summary>Optional RAG — manuals/policies. Unavailable when no corpus exists.</summary>
public interface IAiRagRetriever
{
    bool IsAvailable { get; }

    Task<AiRagRetrievalResult> TryRetrieveAsync(
        int tenantId,
        int userId,
        string query,
        CancellationToken cancellationToken = default);
}

public sealed record AiRagRetrievalResult(
    bool Available,
    IReadOnlyList<AiRagPassage> Passages,
    string? UnavailableReason);

public sealed record AiRagPassage(
    string Source,
    string Text,
    string? PermissionNote);
