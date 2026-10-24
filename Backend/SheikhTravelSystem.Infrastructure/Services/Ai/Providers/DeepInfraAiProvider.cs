using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Providers;

/// <summary>
/// Hosted LLM via DeepInfra OpenAI-compatible API. Recommended model: Qwen/Qwen3-32B.
/// API key from env / user-secrets only (never tenant DB or committed config).
/// Predictions remain rules/stats — this provider is optional language polish only.
/// </summary>
public sealed class DeepInfraAiProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DeepInfraAiProvider> logger) : IAiProvider
{
    public const string HttpClientName = "DeepInfraAi";
    public const string DefaultBaseUrl = "https://api.deepinfra.com/v1/openai";
    /// <summary>DeepInfra model id for Qwen 3 32B.</summary>
    public const string DefaultModel = "Qwen/Qwen3-32B";

    public string ProviderName => "DeepInfra";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex ThinkBlock = new(
        @"<think>[\s\S]*?</think>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<bool> IsAvailableAsync(string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{NormalizeBase(baseUrl)}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "DeepInfra availability check failed");
            return false;
        }
    }

    public async Task<AiProviderChatResult> ChatAsync(
        AiProviderChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();
        var baseUrl = NormalizeBase(request.BaseUrl);
        var apiKey = ResolveApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiProviderChatResult(
                false, string.Empty, ProviderName, model,
                Error: "DeepInfra API key not configured. Set DeepInfra:ApiKey (user-secrets) or DEEPINFRA_API_KEY.");
        }

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            // Qwen3 may spend max_tokens on reasoning; disable thinking for polish replies.
            var payload = new
            {
                model,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens ?? 1024,
                messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
                chat_template_kwargs = new { enable_thinking = false }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
            {
                Content = JsonContent.Create(payload, options: JsonOpts)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await http.SendAsync(httpRequest, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("DeepInfra chat failed ({Status}): {Body}", response.StatusCode, Truncate(raw, 400));
                return new AiProviderChatResult(
                    false, string.Empty, ProviderName, model,
                    Error: $"DeepInfra returned {(int)response.StatusCode}. Check model id and API key.");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var content = string.Empty;
            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var c))
                {
                    content = c.GetString() ?? string.Empty;
                }
            }

            var promptTokens = 0;
            var completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt))
                    promptTokens = pt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var ct))
                    completionTokens = ct.GetInt32();
            }

            content = StripThinking(content);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AiProviderChatResult(false, string.Empty, ProviderName, model,
                    Error: "Empty response from DeepInfra.");
            }

            return new AiProviderChatResult(true, content.Trim(), ProviderName, model, promptTokens, completionTokens);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiProviderChatResult(false, string.Empty, ProviderName, model,
                Error: "DeepInfra timed out. Retry or reduce max tokens.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DeepInfra chat exception");
            return new AiProviderChatResult(false, string.Empty, ProviderName, model,
                Error: $"Cannot reach DeepInfra at {baseUrl}.");
        }
    }

    public bool HasApiKeyConfigured() => !string.IsNullOrWhiteSpace(ResolveApiKey());

    private string? ResolveApiKey()
    {
        var fromConfig = configuration["DeepInfra:ApiKey"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        var fromEnv = Environment.GetEnvironmentVariable("DEEPINFRA_API_KEY");
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
    }

    private static string NormalizeBase(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim().TrimEnd('/');

    private static string StripThinking(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;
        var cleaned = ThinkBlock.Replace(content, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? content.Trim() : cleaned;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
