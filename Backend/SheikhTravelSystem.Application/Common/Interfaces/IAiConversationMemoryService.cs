namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Phase 7 — SQL-backed chat session memory (tenant + user scoped).
/// Reuses AiChatSessions / AiChatMessages; never mixes tenants or users.
/// </summary>
public interface IAiConversationMemoryService
{
    /// <summary>
    /// Ensures a session owned by tenant+user. If <paramref name="sessionId"/> belongs to another
    /// tenant/user, returns a new session (isolation). Tenant change on an owned session starts a
    /// fresh session and sets <see cref="AiConversationSessionEnsureResult.TenantChanged"/>.
    /// </summary>
    Task<AiConversationSessionEnsureResult> EnsureSessionAsync(
        int tenantId,
        int userId,
        Guid? sessionId,
        string? title,
        string firstMessage,
        CancellationToken cancellationToken = default);

    Task AppendMessageAsync(
        Guid sessionId,
        string role,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads history only when session is owned by tenant+user. Empty when unauthorized.
    /// Applies message/token bounds and includes ContextSummary when present.
    /// </summary>
    Task<AiConversationMemorySnapshot> LoadAuthorizedAsync(
        int tenantId,
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task TouchSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// When message count exceeds SummarizeAfterMessages, fold older turns into ContextSummary
    /// (extractive; does not invent facts). Keeps recent messages intact in the table.
    /// </summary>
    Task MaybeSummarizeAsync(
        int tenantId,
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed record AiConversationSessionEnsureResult(
    Guid SessionId,
    bool Created,
    bool TenantChanged,
    string? Notice);

public sealed record AiConversationMemorySnapshot(
    Guid SessionId,
    int TenantId,
    int UserId,
    string? ContextSummary,
    IReadOnlyList<AiChatMessageDto> RecentMessages,
    int TotalMessageCount,
    bool Authorized);
