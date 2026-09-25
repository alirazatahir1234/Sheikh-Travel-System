using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IWhatsAppAiAssistAuditRepository
{
    Task InsertAsync(
        int tenantId,
        int conversationId,
        int? userId,
        string operation,
        string? provider,
        string? model,
        int durationMs,
        bool success,
        string? confidence,
        string? errorCode,
        CancellationToken cancellationToken = default);

    Task<WhatsAppAiActiveTripFactsDto?> GetLatestActiveTripFactsAsync(
        int tenantId,
        string phoneE164,
        int? customerId,
        CancellationToken cancellationToken = default);
}
