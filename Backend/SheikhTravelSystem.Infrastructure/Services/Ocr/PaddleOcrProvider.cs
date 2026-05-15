using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

internal sealed class PaddleOcrServiceResponseDto
{
    [JsonPropertyName("rawText")]
    public string? RawText { get; set; }

    [JsonPropertyName("confidence")]
    public int? Confidence { get; set; }
}

public class PaddleOcrProvider : IOcrProvider
{
    private readonly OcrOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaddleOcrProvider> _logger;

    public PaddleOcrProvider(
        IOptions<OcrOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<PaddleOcrProvider> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderName => "PaddleOCR";

    public async Task<IdentityOcrResult> ExtractAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(fileStream, cancellationToken);

        var baseUrl = _options.PaddleOcrServiceUrl?.Trim();
        if (!string.IsNullOrEmpty(baseUrl))
        {
            try
            {
                var result = await CallPaddleServiceAsync(bytes, fileName, baseUrl, cancellationToken);
                if (result is not null)
                    return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("PaddleOCR HTTP request timed out for {FileName}.", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PaddleOCR HTTP service call failed for {FileName}.", fileName);
            }
        }

        return OcrHeuristics.BuildFromFileName(ProviderName, fileName, confidence: 62);
    }

    private async Task<IdentityOcrResult?> CallPaddleServiceAsync(
        byte[] bytes,
        string fileName,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("PaddleOcr");
        var timeoutSec = Math.Max(5, _options.PaddleOcrTimeoutSeconds);
        client.Timeout = TimeSpan.FromSeconds(timeoutSec);

        var url = $"{baseUrl.TrimEnd('/')}/ocr";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        var ext = Path.GetExtension(fileName);
        var mime = string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
        content.Add(fileContent, "file", Path.GetFileName(fileName) ?? "upload.jpg");

        using var response = await client.PostAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "PaddleOCR service returned {StatusCode} for {Url}.",
                (int)response.StatusCode,
                url);
            return null;
        }

        var dto = await response.Content.ReadFromJsonAsync<PaddleOcrServiceResponseDto>(cancellationToken: cancellationToken);
        if (dto is null || string.IsNullOrWhiteSpace(dto.RawText))
            return null;

        return BuildFromRawText(dto.RawText, dto.Confidence);
    }

    private IdentityOcrResult BuildFromRawText(string rawText, int? serviceConfidence)
    {
        var trimmed = CnicRawTextIdentityParser.NormalizeOcrLayoutForCnic(rawText.Trim());
        var cnic = OcrHeuristics.ExtractAndNormalizeIdentityNumber(trimmed);
        var fullName = CnicRawTextIdentityParser.GuessLatinNameFromRaw(trimmed);
        var address = CnicRawTextIdentityParser.GuessAddressFromRaw(trimmed);

        var confidence = serviceConfidence is > 0 and <= 100
            ? serviceConfidence.Value
            : EstimateConfidence(trimmed, cnic, fullName);

        return new IdentityOcrResult(
            Provider: ProviderName,
            Confidence: confidence,
            FallbackUsed: false,
            FullName: fullName,
            IdentityNumber: cnic,
            Address: address,
            DateOfBirth: CnicRawTextIdentityParser.GuessDateOfBirthFromRaw(trimmed),
            Gender: CnicRawTextIdentityParser.GuessGenderFromRaw(trimmed),
            RawText: trimmed,
            FatherName: CnicRawTextIdentityParser.GuessFatherNameFromRaw(trimmed),
            Nationality: CnicRawTextIdentityParser.GuessNationalityFromRaw(trimmed));
    }

    private static int EstimateConfidence(string raw, string? cnic, string? fullName)
    {
        var score = 55;
        if (!string.IsNullOrEmpty(cnic)) score += 20;
        if (!string.IsNullOrEmpty(fullName)) score += 10;
        if (raw.Length > 80) score += 10;
        return Math.Clamp(score, 1, 99);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
            stream.Position = 0;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
