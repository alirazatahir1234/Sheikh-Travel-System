using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Conversation;

/// <summary>
/// Phase 7 — SQL Server chat memory with tenant/user isolation, bounds, and extractive summarization.
/// </summary>
public sealed class AiConversationMemoryService(
    IDbConnectionFactory dbFactory,
    IOptions<AiConversationContextOptions> options,
    ILogger<AiConversationMemoryService> logger) : IAiConversationMemoryService
{
    private readonly AiConversationContextOptions _opts = options.Value;

    public async Task<AiConversationSessionEnsureResult> EnsureSessionAsync(
        int tenantId,
        int userId,
        Guid? sessionId,
        string? title,
        string firstMessage,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (sessionId is Guid id && id != Guid.Empty)
        {
            var row = await connection.QuerySingleOrDefaultAsync<(int TenantId, int UserId)>(
                new CommandDefinition("""
                    SELECT TenantId, UserId FROM AiChatSessions
                    WHERE Id = @Id AND IsDeleted = 0
                    """, new { Id = id }, cancellationToken: cancellationToken));

            if (row.TenantId == 0 && row.UserId == 0)
            {
                // Not found — fall through to create
            }
            else if (row.UserId != userId)
            {
                logger.LogWarning(
                    "AI session isolation: session={SessionId} user mismatch requester={UserId}",
                    id, userId);
                // Cross-user: never reuse
            }
            else if (row.TenantId != tenantId)
            {
                logger.LogInformation(
                    "AI session tenant change session={SessionId} from={Old} to={New}; starting new session",
                    id, row.TenantId, tenantId);
                var newId = await InsertSessionAsync(
                    connection, tenantId, userId, title, firstMessage, cancellationToken);
                return new AiConversationSessionEnsureResult(
                    newId,
                    Created: true,
                    TenantChanged: true,
                    Notice: "Company scope changed — started a new conversation so earlier answers are not mixed with the new company.");
            }
            else
            {
                return new AiConversationSessionEnsureResult(id, Created: false, TenantChanged: false, Notice: null);
            }
        }

        var created = await InsertSessionAsync(
            connection, tenantId, userId, title, firstMessage, cancellationToken);
        return new AiConversationSessionEnsureResult(created, Created: true, TenantChanged: false, Notice: null);
    }

    public async Task AppendMessageAsync(
        Guid sessionId,
        string role,
        string content,
        CancellationToken cancellationToken = default,
        string? structuredPayload = null,
        string? toolUsed = null)
    {
        // Cap widget JSON — never store secrets; redacted analytics only.
        string? payload = structuredPayload;
        if (payload is { Length: > 180_000 })
            payload = null;
        var tool = string.IsNullOrWhiteSpace(toolUsed) ? null : toolUsed.Trim();
        if (tool is { Length: > 120 })
            tool = tool[..120];

        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AiChatMessages (SessionId, Role, Content, CreatedAt, StructuredPayload, ToolUsed)
            VALUES (@SessionId, @Role, @Content, GETUTCDATE(), @StructuredPayload, @ToolUsed)
            """,
            new
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                StructuredPayload = payload,
                ToolUsed = tool
            },
            cancellationToken: cancellationToken));
    }

    public async Task TouchSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE AiChatSessions SET UpdatedAt = GETUTCDATE() WHERE Id = @Id
            """, new { Id = sessionId }, cancellationToken: cancellationToken));
    }

    public async Task<AiConversationMemorySnapshot> LoadAuthorizedAsync(
        int tenantId,
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var owned = await connection.QuerySingleOrDefaultAsync<(int TenantId, int UserId, string? ContextSummary)>(
            new CommandDefinition("""
                SELECT TenantId, UserId, ContextSummary
                FROM AiChatSessions
                WHERE Id = @Id AND IsDeleted = 0
                """, new { Id = sessionId }, cancellationToken: cancellationToken));

        if (owned.TenantId == 0 && owned.UserId == 0)
            return Unauthorized(sessionId, tenantId, userId);

        if (owned.TenantId != tenantId || owned.UserId != userId)
        {
            logger.LogWarning(
                "AI memory denied session={SessionId} tenant={TenantId} user={UserId}",
                sessionId, tenantId, userId);
            return Unauthorized(sessionId, tenantId, userId);
        }

        var rows = (await connection.QueryAsync<(string Role, string Content)>(new CommandDefinition("""
            SELECT Role, Content FROM AiChatMessages
            WHERE SessionId = @SessionId
            ORDER BY Id ASC
            """, new { SessionId = sessionId }, cancellationToken: cancellationToken))).ToList();

        var all = rows.Select(r => new AiChatMessageDto(r.Role, r.Content)).ToList();
        var recent = BoundMessages(all, _opts.MaxRecentMessages, _opts.MaxApproxTokens);

        return new AiConversationMemorySnapshot(
            sessionId,
            tenantId,
            userId,
            owned.ContextSummary,
            recent,
            all.Count,
            Authorized: true);
    }

    public async Task MaybeSummarizeAsync(
        int tenantId,
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var snap = await LoadAuthorizedAsync(tenantId, userId, sessionId, cancellationToken);
        if (!snap.Authorized)
            return;

        var threshold = Math.Clamp(_opts.SummarizeAfterMessages <= 0 ? 24 : _opts.SummarizeAfterMessages, 8, 200);
        if (snap.TotalMessageCount < threshold)
            return;

        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(long Id, string Role, string Content)>(new CommandDefinition("""
            SELECT Id, Role, Content FROM AiChatMessages
            WHERE SessionId = @SessionId
            ORDER BY Id ASC
            """, new { SessionId = sessionId }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count < threshold)
            return;

        // Keep the newest MaxRecentMessages raw; fold the rest into summary.
        var keep = Math.Clamp(_opts.MaxRecentMessages <= 0 ? 16 : _opts.MaxRecentMessages, 4, 40);
        var older = rows.Take(Math.Max(0, rows.Count - keep)).ToList();
        if (older.Count == 0)
            return;

        var summary = BuildExtractiveSummary(snap.ContextSummary, older, _opts);
        var maxChars = Math.Clamp(_opts.SummaryMaxChars <= 0 ? 2000 : _opts.SummaryMaxChars, 400, 8000);
        if (summary.Length > maxChars)
            summary = summary[..maxChars] + "…";

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE AiChatSessions
            SET ContextSummary = @Summary, SummaryUpdatedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND UserId = @UserId AND IsDeleted = 0
            """,
            new { Id = sessionId, TenantId = tenantId, UserId = userId, Summary = summary },
            cancellationToken: cancellationToken));

        logger.LogInformation(
            "AI conversation summarized session={SessionId} older={OlderCount} total={Total}",
            sessionId, older.Count, rows.Count);
    }

    internal static List<AiChatMessageDto> BoundMessages(
        IReadOnlyList<AiChatMessageDto> all,
        int maxRecent,
        int maxApproxTokens)
    {
        var cap = Math.Clamp(maxRecent <= 0 ? 16 : maxRecent, 4, 40);
        var tokenBudget = Math.Clamp(maxApproxTokens <= 0 ? 6000 : maxApproxTokens, 500, 32_000);

        // Prefer user/assistant turns (skip stray system rows from older builds).
        var rest = all
            .Where(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
            .TakeLast(cap)
            .ToList();

        // Drop oldest until under approx token budget (keep at least last user+assistant pair when possible).
        while (rest.Count > 2 && ApproxTokens(rest) > tokenBudget)
            rest.RemoveAt(0);

        return rest;
    }

    internal static int ApproxTokens(IEnumerable<AiChatMessageDto> messages)
        => messages.Sum(m => ApproxTokens(m.Content));

    internal static int ApproxTokens(string? text)
        => string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    internal static string BuildExtractiveSummary(
        string? existing,
        IReadOnlyList<(long Id, string Role, string Content)> older,
        AiConversationContextOptions opts)
    {
        var perMsg = Math.Clamp(opts.SummaryPerMessageChars <= 0 ? 220 : opts.SummaryPerMessageChars, 80, 800);
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(existing))
            lines.Add(existing.Trim());

        lines.Add("[Earlier turns — conversational memory only; NOT live fleet data]");
        foreach (var row in older)
        {
            var role = string.Equals(row.Role, "user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant";
            var clip = Truncate(row.Content?.Trim() ?? "", perMsg);
            if (string.IsNullOrWhiteSpace(clip))
                continue;
            lines.Add($"- {role}: {clip}");
        }

        return string.Join("\n", lines);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    private static async Task<Guid> InsertSessionAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        int userId,
        string? title,
        string firstMessage,
        CancellationToken ct)
    {
        var newId = Guid.NewGuid();
        var sessionTitle = string.IsNullOrWhiteSpace(title)
            ? (firstMessage.Length > 60 ? firstMessage[..60] + "…" : firstMessage)
            : title!.Trim();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AiChatSessions (Id, TenantId, UserId, Title, CreatedAt, UpdatedAt, IsDeleted)
            VALUES (@Id, @TenantId, @UserId, @Title, GETUTCDATE(), GETUTCDATE(), 0)
            """,
            new { Id = newId, TenantId = tenantId, UserId = userId, Title = sessionTitle },
            cancellationToken: ct));
        return newId;
    }

    private static AiConversationMemorySnapshot Unauthorized(Guid sessionId, int tenantId, int userId)
        => new(sessionId, tenantId, userId, null, [], 0, Authorized: false);
}
