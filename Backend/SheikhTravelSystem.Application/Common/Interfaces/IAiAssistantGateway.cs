namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Phase 2 read-only AI Assistant — allowlisted reporting tools only.
/// Tenant always comes from trusted server context; never from the LLM.
/// </summary>
public interface IAiAssistantGateway
{
    Task<AiAssistantQueryResponse> QueryAsync(
        int tenantId,
        int userId,
        AiAssistantQueryRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> ListAllowlistedTools();
}

public record AiAssistantQueryRequest(
    string Question,
    bool IsPlatformSuperAdmin = false,
    int? VehicleId = null,
    /// <summary>Phase 7 — optional SQL-backed conversation session (tenant+user scoped).</summary>
    Guid? SessionId = null);

public record AiAssistantPeriodDto(DateTime Start, DateTime End);

public record AiAssistantQueryResponse(
    string Answer,
    AiAssistantPeriodDto? Period,
    string? ToolUsed,
    object? Data,
    IReadOnlyList<string> Warnings,
    bool UsedLlm,
    string Mode,
    /// <summary>Phase 7 — conversation session for follow-ups (null when persistence skipped).</summary>
    Guid? SessionId = null,
    /// <summary>Phase 8 — turn correlation id for support / log linkage.</summary>
    string? CorrelationId = null,
    /// <summary>Pending write (e.g. tracker registration ready for CONFIRM).</summary>
    AiPendingActionDto? PendingAction = null);
