using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

public class AzureUrduToEnglishTranslator : IUrduToEnglishTranslator
{
    private readonly TranslatorOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AzureUrduToEnglishTranslator> _logger;

    public AzureUrduToEnglishTranslator(
        IOptions<TranslatorOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AzureUrduToEnglishTranslator> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> TranslateUrduToEnglishAsync(string text, CancellationToken cancellationToken = default)
    {
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (string.IsNullOrWhiteSpace(_options.Key) || string.IsNullOrWhiteSpace(_options.Region))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("AzureTranslator");
            var url = $"{_options.Endpoint.TrimEnd('/')}/translate?api-version=3.0&from=ur&to=en";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _options.Key);
            req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", _options.Region);
            var payload = JsonSerializer.Serialize(new[] { new { Text = t } });
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var resp = await client.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Azure Translator HTTP {Code} for Urdu→English.", (int)resp.StatusCode);
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return null;
            var first = root[0];
            if (!first.TryGetProperty("translations", out var tr) || tr.ValueKind != JsonValueKind.Array || tr.GetArrayLength() == 0)
                return null;
            var textEl = tr[0].GetProperty("text");
            return textEl.GetString()?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Translator failed for Urdu→English.");
            return null;
        }
    }
}
