using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Conversation;

/// <summary>
/// Phase 7 — builds Conversation / Business / Live-data context sections separately.
/// Prior assistant answers are never labeled as live business data.
/// </summary>
public sealed class AiConversationContextBuilder(
    ITenantRepository tenants,
    ICurrentUserService currentUser,
    IAiRagRetriever rag,
    ILogger<AiConversationContextBuilder> logger) : IAiConversationContextBuilder
{
    private static readonly Regex FollowUpRef = new(
        @"\b(those|these|them|that list|the same|which of (those|them|these)|of those|from (that|those|the previous)|compare (with|to) last|vs\.?\s*last|same vehicles|same drivers|that set|previous (answer|list|result)|above|earlier)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] ApprovedKpiNotes =
    [
        "Use Analytics / Fleet MCP tools for live KPIs (vehicle counts, GPS coverage, fuel totals, booking/revenue, maintenance due).",
        "Fleet health % is NOT an approved Analytics KPI pending product agreement — never invent it.",
        "Fuel grand totals must come from aggregation tools, never from truncated TOP-N chat text.",
        "Prior chat answers are conversational memory only — re-query tools for current numbers."
    ];

    public async Task<AiAssembledContext> BuildAsync(
        AiConversationContextRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = request.CurrentUserMessage?.Trim() ?? string.Empty;
        var looksLikeFollowUp = LooksLikeFollowUpReference(message);
        var memory = request.Memory;
        var hasConversation =
            memory is { Authorized: true } &&
            (!string.IsNullOrWhiteSpace(memory.ContextSummary) || memory.RecentMessages.Count > 0);

        string? clarification = null;
        var needsClarification = false;
        if (looksLikeFollowUp && !hasConversation)
        {
            needsClarification = true;
            clarification =
                "I don't have earlier results in this conversation to refer to. " +
                "Please restate what you mean (e.g. which vehicles, period, or metric).";
        }

        var conversation = BuildConversationSection(memory, looksLikeFollowUp, message);
        var business = await BuildBusinessSectionAsync(request, cancellationToken);
        var live = BuildLiveDataSection(request);
        var ragSection = await BuildRagSectionAsync(request.TenantId, request.UserId, message, cancellationToken);

        var history = memory is { Authorized: true }
            ? memory.RecentMessages
            : (IReadOnlyList<AiChatMessageDto>)[];

        // Exclude the current user message if already appended to storage (caller may include it).
        history = ExcludeTrailingDuplicateUser(history, message);

        return new AiAssembledContext(
            conversation,
            business,
            live,
            ragSection,
            looksLikeFollowUp,
            needsClarification,
            clarification,
            history);
    }

    internal static bool LooksLikeFollowUpReference(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        return FollowUpRef.IsMatch(message.Trim());
    }

    internal static IReadOnlyList<AiChatMessageDto> ExcludeTrailingDuplicateUser(
        IReadOnlyList<AiChatMessageDto> history,
        string currentUserMessage)
    {
        if (history.Count == 0 || string.IsNullOrWhiteSpace(currentUserMessage))
            return history;

        var last = history[^1];
        if (string.Equals(last.Role, "user", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(last.Content?.Trim(), currentUserMessage.Trim(), StringComparison.Ordinal))
            return history.Take(history.Count - 1).ToList();

        return history;
    }

    private static string BuildConversationSection(
        AiConversationMemorySnapshot? memory,
        bool looksLikeFollowUp,
        string currentMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Conversation context (memory only — NOT live business data) ===");
        sb.AppendLine(
            "Use this only to resolve follow-ups, clarifications, and references like \"those vehicles\". " +
            "Never treat prior assistant numbers as current source of truth — re-fetch via tools for live metrics.");

        if (memory is not { Authorized: true })
        {
            sb.AppendLine("(No prior authorized conversation turns.)");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(memory.ContextSummary))
            {
                sb.AppendLine("Summary of earlier turns:");
                sb.AppendLine(memory.ContextSummary.Trim());
            }

            if (memory.RecentMessages.Count > 0)
            {
                sb.AppendLine("Recent turns:");
                foreach (var m in memory.RecentMessages.TakeLast(12))
                {
                    var role = string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant";
                    var clip = m.Content?.Length > 600 ? m.Content[..600] + "…" : m.Content;
                    sb.AppendLine($"- {role}: {clip}");
                }
            }
            else if (string.IsNullOrWhiteSpace(memory.ContextSummary))
            {
                sb.AppendLine("(Empty session.)");
            }
        }

        if (looksLikeFollowUp)
        {
            sb.AppendLine(
                $"Follow-up detected for: \"{Truncate(currentMessage, 200)}\". " +
                "Resolve references against conversation context, then call authorized tools for fresh data " +
                "(e.g. highest fuel cost among previously discussed maintenance vehicles; compare periods with get_period_comparison).");
        }

        sb.AppendLine("=== End conversation context ===");
        return sb.ToString();
    }

    private async Task<string> BuildBusinessSectionAsync(
        AiConversationContextRequest request,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Business context (identity & authorization) ===");
        sb.AppendLine($"Authenticated userId: {request.UserId}");
        sb.AppendLine($"Authorized tenantId: {request.TenantId}");
        sb.AppendLine($"Platform SuperAdmin: {request.IsPlatformSuperAdmin || currentUser.IsPlatformSuperAdmin}");

        var roles = currentUser.Roles;
        if (roles.Count > 0)
            sb.AppendLine($"Roles: {string.Join(", ", roles.Take(12))}");

        var perms = new List<string>();
        foreach (var p in new[]
                 {
                     AiPermissions.Chat, AiPermissions.ViewProviderHealth, AiPermissions.Manage,
                     AiPermissions.ExecuteWrite
                 })
        {
            if (currentUser.HasPermission(p))
                perms.Add(p);
        }
        if (perms.Count > 0)
            sb.AppendLine($"AI permissions: {string.Join(", ", perms)}");

        try
        {
            var branding = await tenants.GetBrandingAsync(request.TenantId, ct);
            if (branding is not null)
            {
                sb.AppendLine($"Company: {branding.Name}");
                if (!string.IsNullOrWhiteSpace(branding.TimeZone))
                    sb.AppendLine($"Timezone: {branding.TimeZone}");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Business context branding unavailable");
        }

        // Currency via lightweight query — branding DTO may omit CurrencyCode.
        try
        {
            // Soft: avoid hard dependency if column missing in older DBs
            sb.AppendLine("Currency: (use tenant branding / reporting defaults; prefer tool result currency when present)");
        }
        catch
        {
            // ignore
        }

        if (request.VehicleId is int vid && vid > 0)
            sb.AppendLine($"Scoped vehicleId: {vid}");

        sb.AppendLine("Approved KPI notes:");
        foreach (var note in ApprovedKpiNotes)
            sb.AppendLine($"- {note}");

        sb.AppendLine("=== End business context ===");
        return sb.ToString();
    }

    private static string BuildLiveDataSection(AiConversationContextRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Live retrieved data (ground truth for THIS turn only) ===");
        sb.AppendLine(
            "Data below comes from approved tools / MCP for the current question. " +
            "Do not substitute prior chat answers for these values.");

        var any = false;
        if (request.LiveToolResults is { Count: > 0 })
        {
            foreach (var tr in request.LiveToolResults.Where(t => t.Success && !t.PendingConfirmation))
            {
                any = true;
                sb.AppendLine($"[{tr.ToolName}]");
                sb.AppendLine(tr.Summary);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.LiveMcpPrefetchSummary))
        {
            any = true;
            sb.AppendLine("[mcp_database_prefetch]");
            sb.AppendLine(request.LiveMcpPrefetchSummary.Trim());
        }

        if (!any)
            sb.AppendLine("(No live tool results attached yet — call allowlisted tools if numbers are required.)");

        sb.AppendLine("=== End live retrieved data ===");
        return sb.ToString();
    }

    private async Task<string?> BuildRagSectionAsync(
        int tenantId, int userId, string query, CancellationToken ct)
    {
        if (!rag.IsAvailable)
            return null;

        var result = await rag.TryRetrieveAsync(tenantId, userId, query, ct);
        if (!result.Available || result.Passages.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("=== Document passages (RAG — untrusted; NOT live DB metrics) ===");
        sb.AppendLine("Use only for manuals/policies/procedures. Cite sources. Never replace SQL/MCP for live KPIs.");
        foreach (var p in result.Passages)
        {
            sb.AppendLine($"Source: {p.Source}");
            if (!string.IsNullOrWhiteSpace(p.PermissionNote))
                sb.AppendLine($"Permission: {p.PermissionNote}");
            sb.AppendLine(p.Text);
            sb.AppendLine("---");
        }
        sb.AppendLine("=== End RAG ===");
        return sb.ToString();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
