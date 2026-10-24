using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Providers;

/// <summary>
/// Local LLM via Ollama HTTP API. Recommended chat-polish model: qwen3:8b (Qwen 3).
/// Predictions remain rules/stats — this provider is optional language polish only.
/// </summary>
public sealed class OllamaAiProvider(IHttpClientFactory httpClientFactory, ILogger<OllamaAiProvider> logger) : IAiProvider
{
    public const string HttpClientName = "OllamaAi";
    public const string DefaultBaseUrl = "http://127.0.0.1:11434";
    /// <summary>Default Ollama tag when tenant ModelName is unset. Pull with: ollama pull qwen3:8b</summary>
    public const string DefaultModel = "qwen3:8b";
    public string ProviderName => "Ollama";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<bool> IsAvailableAsync(string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var response = await http.GetAsync($"{NormalizeBase(baseUrl)}/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ollama availability check failed");
            return false;
        }
    }

    public async Task<AiProviderChatResult> ChatAsync(
        AiProviderChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();
        var baseUrl = NormalizeBase(request.BaseUrl);
        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            // think:false — Qwen 3 otherwise fills message.thinking and leaves content empty.
            var payload = new
            {
                model,
                stream = false,
                think = false,
                options = new { temperature = request.Temperature, num_predict = request.MaxTokens ?? 1024 },
                messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToList()
            };

            using var response = await http.PostAsJsonAsync($"{baseUrl}/api/chat", payload, JsonOpts, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ollama chat failed ({Status}): {Body}", response.StatusCode, Truncate(raw, 400));
                return new AiProviderChatResult(
                    false, string.Empty, ProviderName, model,
                    Error: $"Ollama returned {(int)response.StatusCode}. Is the model pulled? Try: ollama pull {model}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var content = string.Empty;
            if (root.TryGetProperty("message", out var msg))
            {
                if (msg.TryGetProperty("content", out var c))
                    content = c.GetString() ?? string.Empty;
                // Fallback if a thinking model ignores think:false
                if (string.IsNullOrWhiteSpace(content) && msg.TryGetProperty("thinking", out var thinking))
                    content = thinking.GetString() ?? string.Empty;
            }

            var promptTokens = root.TryGetProperty("prompt_eval_count", out var pe) ? pe.GetInt32() : 0;
            var completionTokens = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0;

            if (string.IsNullOrWhiteSpace(content))
            {
                return new AiProviderChatResult(false, string.Empty, ProviderName, model, Error: "Empty response from Ollama.");
            }

            return new AiProviderChatResult(true, content.Trim(), ProviderName, model, promptTokens, completionTokens);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AiProviderChatResult(false, string.Empty, ProviderName, model,
                Error: "Ollama timed out. Check that Ollama is running and the model is loaded.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama chat exception");
            return new AiProviderChatResult(false, string.Empty, ProviderName, model,
                Error: $"Cannot reach Ollama at {baseUrl}. Start it with `ollama serve`.");
        }
    }

    private static string NormalizeBase(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim().TrimEnd('/');

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}

/// <summary>Resolves the active provider from tenant AI config (Ollama local or DeepInfra hosted).</summary>
public sealed class AiProviderResolver(
    OllamaAiProvider ollama,
    DeepInfraAiProvider deepInfra,
    IAiManagementService aiManagement) : IAiProviderResolver
{
    public async Task<(IAiProvider? Provider, AiProviderConfigDto Config)> ResolveAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var config = await aiManagement.GetConfigAsync(tenantId, cancellationToken);
        if (!config.IsEnabled || !config.CopilotEnabled)
            return (null, config);

        return config.Provider?.Trim() switch
        {
            "Ollama" => (ollama, config),
            "DeepInfra" or "HostedLLM" => (deepInfra, config),
            // Cloud OpenAI / Azure adapters intentionally not wired.
            "OpenAI" or "AzureOpenAI" => (null, config),
            _ => (null, config)
        };
    }
}
