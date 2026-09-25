using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Phase 1–4 inbox extras (webhook logs, retry, CRM context). Kept off
/// <see cref="IWhatsAppInboxRepository"/> to avoid blast radius on the core inbox port.
/// </summary>
public interface IWhatsAppInboxExtended
{
    Task<long> InsertWebhookLogAsync(
        bool signatureValid,
        string payload,
        string processingStatus,
        string? phoneNumberId = null,
        int? tenantId = null,
        string? error = null,
        CancellationToken ct = default);

    Task MarkWebhookLogProcessingAsync(long id, CancellationToken ct = default);

    Task MarkWebhookLogSucceededAsync(long id, CancellationToken ct = default);

    Task MarkWebhookLogFailedAsync(long id, string error, CancellationToken ct = default);

    Task<int> IncrementWebhookLogAttemptAsync(long id, string error, CancellationToken ct = default);

    Task<(IReadOnlyList<WhatsAppWebhookLogDto> Items, int Total)> GetWebhookLogsAsync(
        int page,
        int pageSize,
        string? status = null,
        CancellationToken ct = default);

    Task<WhatsAppWebhookLogDto?> GetWebhookLogAsync(long id, CancellationToken ct = default);

    Task<string?> GetWebhookLogPayloadAsync(long id, CancellationToken ct = default);

    Task DeleteOldWebhookLogsAsync(int retentionDays, CancellationToken ct = default);

    Task SetConversationWindowExpiresAtAsync(
        int conversationId, DateTime? windowExpiresAtUtc, CancellationToken ct = default);

    Task<WhatsAppOutboundMessageRow?> GetOutboundMessageForRetryAsync(
        int tenantId, int messageId, CancellationToken ct = default);

    Task IncrementMessageAttemptAsync(
        int tenantId, int messageId, string? newMetaMessageId, string status, CancellationToken ct = default);

    Task<WhatsAppConversationContextDto?> GetConversationContextAsync(
        int tenantId, int conversationId, CancellationToken ct = default);
}

public sealed record WhatsAppOutboundMessageRow(
    int Id,
    int ConversationId,
    int AccountId,
    string? MessageId,
    string MessageType,
    string? Text,
    string? TemplateName,
    string Status,
    int AttemptCount,
    string? ErrorMessage);
