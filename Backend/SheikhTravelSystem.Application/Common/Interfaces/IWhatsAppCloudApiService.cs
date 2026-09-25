using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IWhatsAppCloudApiService
{
    Task<WhatsAppCloudApiResult> SendTextAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string body,
        CancellationToken cancellationToken = default);

    Task<WhatsAppCloudApiResult> SendTemplateAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string templateName,
        string languageCode,
        IReadOnlyList<string>? bodyParameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Foundation send for image/video/audio/document via Meta media id or public link.</summary>
    Task<WhatsAppCloudApiResult> SendMediaAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string mediaType,
        string mediaIdOrLink,
        string? caption = null,
        CancellationToken cancellationToken = default);

    /// <summary>Meta interactive list message (up to 10 rows). Soft-fails to caller for text fallback.</summary>
    Task<WhatsAppCloudApiResult> SendInteractiveListAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string bodyText,
        string buttonLabel,
        string sectionTitle,
        IReadOnlyList<string> options,
        CancellationToken cancellationToken = default);

    Task<WhatsAppCloudApiResult> MarkMessageReadAsync(
        WhatsAppAccountRow account,
        string incomingMetaMessageId,
        CancellationToken cancellationToken = default);

    Task<WhatsAppMediaProxyResult?> DownloadMediaAsync(
        WhatsAppAccountRow account,
        string mediaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight Graph GET on the phone-number-id to validate credentials without sending a message.
    /// </summary>
    Task<WhatsAppCloudApiResult> CheckPhoneNumberAsync(
        WhatsAppAccountRow account,
        CancellationToken cancellationToken = default);
}

public enum WhatsAppCloudErrorKind
{
    None = 0,
    InvalidConfiguration = 1,
    InvalidToken = 2,
    InvalidPhoneNumberId = 3,
    InvalidRecipient = 4,
    RateLimited = 5,
    TemplateError = 6,
    NetworkFailure = 7,
    UnexpectedResponse = 8,
    MetaApiError = 9
}

public sealed record WhatsAppCloudApiResult(
    bool Success,
    string? MetaMessageId,
    WhatsAppCloudErrorKind ErrorKind,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static WhatsAppCloudApiResult Ok(string? metaMessageId)
        => new(true, metaMessageId, WhatsAppCloudErrorKind.None, null, null);

    public static WhatsAppCloudApiResult Fail(
        WhatsAppCloudErrorKind kind,
        string? message,
        string? code = null)
        => new(false, null, kind, code, message);
}
