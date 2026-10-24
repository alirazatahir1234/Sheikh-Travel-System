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

public record AiAssistantQueryRequest(string Question, bool IsPlatformSuperAdmin = false);

public record AiAssistantPeriodDto(DateTime Start, DateTime End);

public record AiAssistantQueryResponse(
    string Answer,
    AiAssistantPeriodDto Period,
    string? ToolUsed,
    object? Data,
    IReadOnlyList<string> Warnings,
    bool UsedLlm,
    string Mode);
