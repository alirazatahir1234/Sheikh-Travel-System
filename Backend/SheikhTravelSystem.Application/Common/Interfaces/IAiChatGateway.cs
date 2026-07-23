namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>Provider-agnostic LLM chat (OpenAI, Azure, Ollama, etc.).</summary>
public interface IAiProvider
{
    string ProviderName { get; }

    Task<AiProviderChatResult> ChatAsync(
        AiProviderChatRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(string? baseUrl = null, CancellationToken cancellationToken = default);
}

public record AiProviderChatRequest(
    string Model,
    IReadOnlyList<AiChatMessageDto> Messages,
    string? BaseUrl = null,
    double Temperature = 0.3,
    int? MaxTokens = 1024);

public record AiChatMessageDto(string Role, string Content);

public record AiProviderChatResult(
    bool Success,
    string Content,
    string Provider,
    string Model,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    string? Error = null);

/// <summary>SheikhGo AI Gateway — memory + provider + rule fallback. Phase 1 = chat only.</summary>
public interface IAiChatGateway
{
    Task<AiChatTurnResponse> ChatAsync(
        int tenantId,
        int userId,
        AiChatTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiChatSessionDto>> ListSessionsAsync(
        int tenantId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiChatMessageDto>> GetMessagesAsync(
        int tenantId,
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<AiProviderHealthDto> GetProviderHealthAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<AiPendingActionDto?> GetPendingActionAsync(
        int tenantId,
        int userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public record AiChatTurnRequest(
    string Message,
    Guid? SessionId = null,
    string? Title = null,
    bool ConfirmWrite = false);

public record AiChatTurnResponse(
    Guid SessionId,
    string Answer,
    string Mode,
    bool UsedLlm,
    string Provider,
    string? Model,
    IReadOnlyList<string> SuggestedPrompts,
    IReadOnlyList<string> ToolsUsed,
    AiPendingActionDto? PendingAction = null);

public record AiPendingActionDto(
    string ToolName,
    string Summary,
    DateTime ExpiresAt);

public record AiChatSessionDto(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount);

public record AiProviderHealthDto(
    string Provider,
    string? Model,
    string? Endpoint,
    bool Configured,
    bool Reachable,
    string StatusMessage);
