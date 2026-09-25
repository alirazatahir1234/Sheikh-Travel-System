using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

public sealed class WhatsAppCloudApiService(
    IHttpClientFactory httpClientFactory,
    IWhatsAppAccountConfig accountConfig,
    ILogger<WhatsAppCloudApiService> logger) : IWhatsAppCloudApiService
{
    public const string HttpClientName = "WhatsAppCloud";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image", "video", "audio", "document"
    };

    public Task<WhatsAppCloudApiResult> SendTextAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string body,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toE164Digits,
            type = "text",
            text = new { preview_url = false, body }
        };
        return PostMessageAsync(account, payload, cancellationToken);
    }

    public Task<WhatsAppCloudApiResult> SendTemplateAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string templateName,
        string languageCode,
        IReadOnlyList<string>? bodyParameters = null,
        CancellationToken cancellationToken = default)
    {
        object? components = null;
        if (bodyParameters is { Count: > 0 })
        {
            components = new[]
            {
                new
                {
                    type = "body",
                    parameters = bodyParameters.Select(t => new { type = "text", text = t }).ToArray()
                }
            };
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = toE164Digits,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components
            }
        };
        return PostMessageAsync(account, payload, cancellationToken);
    }

    public Task<WhatsAppCloudApiResult> SendMediaAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string mediaType,
        string mediaIdOrLink,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaType) || !MediaTypes.Contains(mediaType.Trim()))
        {
            return Task.FromResult(WhatsAppCloudApiResult.Fail(
                WhatsAppCloudErrorKind.InvalidConfiguration,
                "mediaType must be image, video, audio, or document."));
        }

        if (string.IsNullOrWhiteSpace(mediaIdOrLink))
        {
            return Task.FromResult(WhatsAppCloudApiResult.Fail(
                WhatsAppCloudErrorKind.InvalidConfiguration,
                "mediaIdOrLink is required."));
        }

        var type = mediaType.Trim().ToLowerInvariant();
        var isLink = mediaIdOrLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || mediaIdOrLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        object mediaObject = isLink
            ? string.IsNullOrWhiteSpace(caption)
                ? new { link = mediaIdOrLink }
                : new { link = mediaIdOrLink, caption }
            : string.IsNullOrWhiteSpace(caption)
                ? new { id = mediaIdOrLink }
                : new { id = mediaIdOrLink, caption };

        // Build payload with dynamic media property name matching type.
        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toE164Digits,
            ["type"] = type,
            [type] = mediaObject
        };
        return PostMessageAsync(account, payload, cancellationToken);
    }

    public Task<WhatsAppCloudApiResult> SendInteractiveListAsync(
        WhatsAppAccountRow account,
        string toE164Digits,
        string bodyText,
        string buttonLabel,
        string sectionTitle,
        IReadOnlyList<string> options,
        CancellationToken cancellationToken = default)
    {
        if (options is null || options.Count == 0)
        {
            return Task.FromResult(WhatsAppCloudApiResult.Fail(
                WhatsAppCloudErrorKind.InvalidConfiguration,
                "Interactive list requires at least one option."));
        }

        var rows = options
            .Take(10)
            .Select((label, i) => new
            {
                id = $"opt_{i + 1}",
                title = label.Length <= 24 ? label : label[..24],
                description = label.Length > 24 ? label[..Math.Min(72, label.Length)] : (string?)null
            })
            .ToArray();

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toE164Digits,
            type = "interactive",
            interactive = new
            {
                type = "list",
                body = new { text = string.IsNullOrWhiteSpace(bodyText) ? "Please choose:" : bodyText },
                action = new
                {
                    button = string.IsNullOrWhiteSpace(buttonLabel)
                        ? "Options"
                        : (buttonLabel.Length <= 20 ? buttonLabel : buttonLabel[..20]),
                    sections = new[]
                    {
                        new
                        {
                            title = string.IsNullOrWhiteSpace(sectionTitle)
                                ? "Options"
                                : (sectionTitle.Length <= 24 ? sectionTitle : sectionTitle[..24]),
                            rows
                        }
                    }
                }
            }
        };
        return PostMessageAsync(account, payload, cancellationToken);
    }

    public Task<WhatsAppCloudApiResult> MarkMessageReadAsync(
        WhatsAppAccountRow account,
        string incomingMetaMessageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(incomingMetaMessageId))
        {
            return Task.FromResult(WhatsAppCloudApiResult.Fail(
                WhatsAppCloudErrorKind.InvalidConfiguration,
                "Incoming message id is required to mark as read."));
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = incomingMetaMessageId.Trim()
        };
        return PostMessageAsync(account, payload, expectMessageId: false, cancellationToken);
    }

    public async Task<WhatsAppCloudApiResult> CheckPhoneNumberAsync(
        WhatsAppAccountRow account,
        CancellationToken cancellationToken = default)
    {
        var credentials = accountConfig.ResolveCredentials(account);
        if (credentials is null)
        {
            return WhatsAppCloudApiResult.Fail(
                WhatsAppCloudErrorKind.InvalidConfiguration,
                "WhatsApp account credentials are not configured (PhoneNumberId / AccessToken).");
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var path = $"{credentials.PhoneNumberId}?fields=id,display_phone_number,verified_name";
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return MapErrorResponse(response.StatusCode, raw, credentials.AccessToken);

            using var doc = JsonDocument.Parse(raw);
            var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return WhatsAppCloudApiResult.Fail(
                    WhatsAppCloudErrorKind.UnexpectedResponse,
                    "Meta response did not include a phone number id.");
            }

            var display = doc.RootElement.TryGetProperty("display_phone_number", out var dEl)
                ? dEl.GetString()
                : null;
            var verified = doc.RootElement.TryGetProperty("verified_name", out var vEl)
                ? vEl.GetString()
                : null;

            var summary = string.Join(" · ", new[] { display, verified }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return new WhatsAppCloudApiResult(
                true,
                id,
                WhatsAppCloudErrorKind.None,
                null,
                string.IsNullOrWhiteSpace(summary)
                    ? "Phone number credentials verified with Meta."
                    : $"Verified: {summary}");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp health check timed out. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.NetworkFailure, "WhatsApp Graph request timed out.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "WhatsApp health check network failure. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.NetworkFailure, "WhatsApp Graph network failure.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "WhatsApp health check unexpected JSON. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.UnexpectedResponse, "Unexpected Meta response.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "WhatsApp health check unexpected error. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.UnexpectedResponse, "Unexpected error calling Meta WhatsApp API.");
        }
    }

    public async Task<WhatsAppMediaProxyResult?> DownloadMediaAsync(
        WhatsAppAccountRow account,
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        var credentials = accountConfig.ResolveCredentials(account);
        if (credentials is null)
        {
            logger.LogWarning("WhatsApp media download skipped: credentials not configured for account {Code}", account.Code);
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var metaReq = new HttpRequestMessage(HttpMethod.Get, mediaId.TrimStart('/'));
            metaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            using var metaResp = await client.SendAsync(metaReq, cancellationToken);
            if (!metaResp.IsSuccessStatusCode)
            {
                logger.LogWarning("WhatsApp media metadata failed. Status={Status} MediaId={MediaId}",
                    (int)metaResp.StatusCode, mediaId);
                return null;
            }

            var meta = await metaResp.Content.ReadFromJsonAsync<MediaMetaResponse>(cancellationToken);
            var url = meta?.Url?.Trim();
            if (string.IsNullOrWhiteSpace(url)) return null;

            using var binReq = new HttpRequestMessage(HttpMethod.Get, url);
            binReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            using var binResp = await client.SendAsync(binReq, cancellationToken);
            if (!binResp.IsSuccessStatusCode) return null;

            var bytes = await binResp.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = binResp.Content.Headers.ContentType?.MediaType
                              ?? meta?.MimeType
                              ?? "application/octet-stream";
            return new WhatsAppMediaProxyResult(bytes, contentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "WhatsApp media download network failure for MediaId={MediaId}", mediaId);
            return null;
        }
    }

    private Task<WhatsAppCloudApiResult> PostMessageAsync(
        WhatsAppAccountRow account,
        object payload,
        CancellationToken cancellationToken)
        => PostMessageAsync(account, payload, expectMessageId: true, cancellationToken);

    private async Task<WhatsAppCloudApiResult> PostMessageAsync(
        WhatsAppAccountRow account,
        object payload,
        bool expectMessageId,
        CancellationToken cancellationToken)
    {
        var credentials = accountConfig.ResolveCredentials(account);
        if (credentials is null)
        {
            return WhatsAppCloudApiResult.Fail(
                WhatsAppCloudErrorKind.InvalidConfiguration,
                "WhatsApp account credentials are not configured (PhoneNumberId / AccessToken).");
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{credentials.PhoneNumberId}/messages")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return MapErrorResponse(response.StatusCode, raw, credentials.AccessToken);

            if (!expectMessageId)
                return WhatsAppCloudApiResult.Ok(null);

            using var doc = JsonDocument.Parse(raw);
            string? messageId = null;
            if (doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var idEl))
            {
                messageId = idEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(messageId))
            {
                logger.LogWarning("WhatsApp send succeeded but response missing message id. Account={Code}", account.Code);
                return WhatsAppCloudApiResult.Fail(
                    WhatsAppCloudErrorKind.UnexpectedResponse,
                    "Meta response did not include a message id.");
            }

            return WhatsAppCloudApiResult.Ok(messageId);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp Graph request timed out. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.NetworkFailure, "WhatsApp Graph request timed out.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "WhatsApp Graph network failure. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.NetworkFailure, "WhatsApp Graph network failure.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "WhatsApp Graph unexpected JSON. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.UnexpectedResponse, "Unexpected Meta response.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "WhatsApp Graph unexpected error. Account={Code}", account.Code);
            return WhatsAppCloudApiResult.Fail(WhatsAppCloudErrorKind.UnexpectedResponse, "Unexpected error calling Meta WhatsApp API.");
        }
    }

    private WhatsAppCloudApiResult MapErrorResponse(HttpStatusCode statusCode, string raw, string accessToken)
    {
        var (code, message, type) = ParseMetaError(raw);
        var safeMessage = Sanitize(message ?? Truncate(raw, 400), accessToken);
        var kind = ClassifyError(statusCode, code, message, type);

        logger.LogWarning(
            "WhatsApp Graph error. Status={Status} ErrorKind={ErrorKind} MetaCode={MetaCode} Message={Message}",
            (int)statusCode, kind, code, safeMessage);

        return WhatsAppCloudApiResult.Fail(kind, safeMessage, code);
    }

    public static WhatsAppCloudErrorKind ClassifyError(
        HttpStatusCode statusCode,
        string? code,
        string? message,
        string? type)
    {
        if (statusCode == HttpStatusCode.TooManyRequests)
            return WhatsAppCloudErrorKind.RateLimited;

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return WhatsAppCloudErrorKind.InvalidToken;

        var msg = message ?? "";
        var codeNum = int.TryParse(code, out var n) ? n : (int?)null;

        if (ContainsAny(msg, "access token", "oauth", "session has expired")
            || codeNum is 190 or 102)
            return WhatsAppCloudErrorKind.InvalidToken;

        if (ContainsAny(msg, "template", "message template")
            || codeNum is 132000 or 132001 or 132005 or 132007 or 132012 or 132015 or 132016)
            return WhatsAppCloudErrorKind.TemplateError;

        if (ContainsAny(msg, "recipient", "not a valid whatsapp", "invalid phone number", "undeliverable")
            || codeNum is 131026 or 131047 or 131048)
            return WhatsAppCloudErrorKind.InvalidRecipient;

        if (ContainsAny(msg, "rate limit", "throughput", "too many")
            || codeNum is 4 or 80007 or 130429)
            return WhatsAppCloudErrorKind.RateLimited;

        if (statusCode == HttpStatusCode.NotFound
            || ContainsAny(msg, "phone number id", "phone_number_id")
            || (codeNum is 100 && ContainsAny(msg, "phone", "number id")))
            return WhatsAppCloudErrorKind.InvalidPhoneNumberId;

        _ = type;
        return WhatsAppCloudErrorKind.MetaApiError;
    }

    private static (string? Code, string? Message, string? Type) ParseMetaError(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("error", out var err))
                return (null, Truncate(raw, 400), null);

            string? code = null;
            if (err.TryGetProperty("code", out var codeEl))
                code = codeEl.ValueKind == JsonValueKind.Number
                    ? codeEl.GetInt32().ToString()
                    : codeEl.GetString();

            var message = err.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
            var type = err.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            return (code, message, type);
        }
        catch (JsonException)
        {
            return (null, Truncate(raw, 400), null);
        }
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static string Sanitize(string? text, string accessToken)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var result = text;
        if (!string.IsNullOrWhiteSpace(accessToken) && result.Contains(accessToken, StringComparison.Ordinal))
            result = result.Replace(accessToken, "[redacted]", StringComparison.Ordinal);
        if (result.Contains("Bearer ", StringComparison.OrdinalIgnoreCase))
            result = System.Text.RegularExpressions.Regex.Replace(
                result, @"Bearer\s+\S+", "Bearer [redacted]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return Truncate(result, 400);
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        return value.Length <= max ? value : value[..max];
    }

    private sealed class MediaMetaResponse
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }
    }
}
