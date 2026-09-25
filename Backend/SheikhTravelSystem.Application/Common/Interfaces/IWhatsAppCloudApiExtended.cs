using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Phase 2–3 Meta extras (interactive rows/buttons, template sync, template button params).
/// Kept off <see cref="IWhatsAppCloudApiService"/> to avoid blast radius on the core send surface.
/// </summary>
public interface IWhatsAppCloudApiExtended
{
    Task<WhatsAppCloudApiResult> SendTemplateAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string templateName,
        string languageCode,
        IReadOnlyList<string>? bodyParameters = null,
        string? urlButtonSuffix = null,
        IReadOnlyList<string>? quickReplyPayloads = null,
        CancellationToken cancellationToken = default);

    Task<WhatsAppCloudApiResult> SendInteractiveListRowsAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string bodyText,
        string buttonLabel,
        string sectionTitle,
        IReadOnlyList<WhatsAppInteractiveRow> rows,
        CancellationToken cancellationToken = default);

    Task<WhatsAppCloudApiResult> SendInteractiveReplyButtonsAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string bodyText,
        IReadOnlyList<WhatsAppInteractiveRow> buttons,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhatsAppMetaTemplateDto>> ListMessageTemplatesAsync(
        WhatsAppAccountRow account,
        CancellationToken cancellationToken = default);
}

public sealed record WhatsAppMetaTemplateDto(
    string Name,
    string Language,
    string Status,
    string Category,
    string? MetaTemplateId,
    string? BodyPreview);

/// <summary>Interactive list row or reply button (id must be stable for FSM matching).</summary>
public sealed record WhatsAppInteractiveRow(string Id, string Title);
