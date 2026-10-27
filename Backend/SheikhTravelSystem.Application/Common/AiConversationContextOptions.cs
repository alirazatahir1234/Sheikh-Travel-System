namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Phase 7 — bounded conversation memory + context assembly (Ai:ConversationContext).
/// </summary>
public sealed class AiConversationContextOptions
{
    public const string SectionName = "Ai:ConversationContext";

    /// <summary>Max non-system recent turns kept in the LLM seed (clamped 4–40).</summary>
    public int MaxRecentMessages { get; set; } = 16;

    /// <summary>Approximate token budget for conversation history (chars/4 heuristic).</summary>
    public int MaxApproxTokens { get; set; } = 6_000;

    /// <summary>When stored messages exceed this, older turns are folded into ContextSummary.</summary>
    public int SummarizeAfterMessages { get; set; } = 24;

    /// <summary>Max characters stored in AiChatSessions.ContextSummary.</summary>
    public int SummaryMaxChars { get; set; } = 2_000;

    /// <summary>Max chars kept per prior assistant message inside the extractive summary.</summary>
    public int SummaryPerMessageChars { get; set; } = 220;
}
