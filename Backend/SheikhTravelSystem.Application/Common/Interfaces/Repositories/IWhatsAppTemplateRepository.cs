using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IWhatsAppTemplateRepository
{
    Task<IReadOnlyList<WhatsAppTemplateDto>> ListAsync(
        int tenantId,
        int? accountId = null,
        string? status = null,
        CancellationToken ct = default);

    Task<WhatsAppTemplateDto?> GetByIdAsync(int tenantId, int id, CancellationToken ct = default);

    Task<WhatsAppTemplateDto?> GetByNameAsync(
        int tenantId,
        int accountId,
        string name,
        string language,
        CancellationToken ct = default);

    Task<WhatsAppTemplateDto?> UpsertAsync(
        int tenantId,
        int accountId,
        string name,
        string language,
        string category,
        string status,
        string? metaTemplateId,
        string? bodyPreview,
        CancellationToken ct = default);

    Task<WhatsAppTemplateDto?> SetStatusAsync(
        int tenantId,
        int id,
        string status,
        string? metaTemplateId = null,
        CancellationToken ct = default);
}
