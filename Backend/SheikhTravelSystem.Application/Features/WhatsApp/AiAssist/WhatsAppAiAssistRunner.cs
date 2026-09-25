using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.AiAssist;

/// <summary>
/// Phase 4.1 agent assist — suggestions only; never sends customer-facing WhatsApp messages.
/// Concrete Application runner (no Common interface) to keep Code Impact blast radius low.
/// </summary>
public sealed class WhatsAppAiAssistRunner(
    IWhatsAppInboxRepository inbox,
    IWhatsAppInboxExtended inboxExtended,
    IWhatsAppAiAssistAuditRepository audit,
    IAiProviderResolver providerResolver,
    IAiManagementService aiManagement,
    ILogger<WhatsAppAiAssistRunner> logger)
{
    public Task<ApiResponse<WhatsAppAiAssistResultDto>> SuggestAsync(
        int tenantId,
        int userId,
        int conversationId,
        WhatsAppAiAssistSuggestRequest request,
        CancellationToken cancellationToken = default)
        => RunAsync(
            tenantId,
            userId,
            conversationId,
            string.IsNullOrWhiteSpace(request.PriorSuggestion)
                ? WhatsAppAiAssistOperations.Suggest
                : WhatsAppAiAssistOperations.Regenerate,
            systemPrompt: WhatsAppAiAssistPrompt.SystemPromptSuggest,
            buildUserPrompt: facts => WhatsAppAiAssistPrompt.BuildSuggestUserPrompt(
                facts, request.PriorSuggestion, request.Instruction),
            cancellationToken);

    public Task<ApiResponse<WhatsAppAiAssistResultDto>> TransformAsync(
        int tenantId,
        int userId,
        int conversationId,
        WhatsAppAiAssistTransformRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = WhatsAppAiAssistPrompt.NormalizeTransformAction(request.Action);
        if (action.Equals(WhatsAppAiAssistOperations.Regenerate, StringComparison.OrdinalIgnoreCase))
        {
            return SuggestAsync(
                tenantId,
                userId,
                conversationId,
                new WhatsAppAiAssistSuggestRequest(request.PriorSuggestion ?? request.Text, null),
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.Text) &&
            !action.Equals(WhatsAppAiAssistOperations.Translate, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                "Text is required for this transform action.", code: "AI_ASSIST_TEXT_REQUIRED"));
        }

        var transformLabel = action switch
        {
            WhatsAppAiAssistOperations.Shorter => "Make shorter while keeping meaning",
            WhatsAppAiAssistOperations.Professional => "Make more professional and polite",
            WhatsAppAiAssistOperations.Translate =>
                $"Translate to {(string.IsNullOrWhiteSpace(request.TargetLanguage) ? "English" : request.TargetLanguage.Trim())}",
            _ => action
        };

        var text = request.Text ?? request.PriorSuggestion ?? "";
        return RunAsync(
            tenantId,
            userId,
            conversationId,
            action,
            systemPrompt: WhatsAppAiAssistPrompt.SystemPromptTransform(transformLabel, request.TargetLanguage),
            buildUserPrompt: facts => WhatsAppAiAssistPrompt.BuildTransformUserPrompt(text, facts),
            cancellationToken);
    }

    public Task<ApiResponse<WhatsAppAiAssistResultDto>> SummarizeAsync(
        int tenantId,
        int userId,
        int conversationId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            tenantId,
            userId,
            conversationId,
            WhatsAppAiAssistOperations.Summary,
            systemPrompt: WhatsAppAiAssistPrompt.SystemPromptSummary,
            buildUserPrompt: WhatsAppAiAssistPrompt.BuildSummaryUserPrompt,
            cancellationToken);

    private async Task<ApiResponse<WhatsAppAiAssistResultDto>> RunAsync(
        int tenantId,
        int userId,
        int conversationId,
        string operation,
        string systemPrompt,
        Func<string, string> buildUserPrompt,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        string? providerName = null;
        string? model = null;

        try
        {
            var conversation = await inbox.GetConversationAsync(tenantId, conversationId, cancellationToken);
            if (conversation is null)
            {
                await LogAsync(tenantId, conversationId, userId, operation, null, null, sw, false, null, "NOT_FOUND", cancellationToken);
                return ApiResponse<WhatsAppAiAssistResultDto>.FailResponse("Conversation not found.", code: "NOT_FOUND");
            }

            var context = await inboxExtended.GetConversationContextAsync(tenantId, conversationId, cancellationToken);
            var (msgPage, _) = await inbox.GetMessagesAsync(tenantId, conversationId, 1, WhatsAppAiAssistPrompt.MaxMessages, cancellationToken);
            var messages = msgPage.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id).ToList();

            var activeTrip = await audit.GetLatestActiveTripFactsAsync(
                tenantId,
                conversation.ContactPhone,
                conversation.CustomerId ?? context?.CustomerId,
                cancellationToken);

            var facts = WhatsAppAiAssistPrompt.BuildFactsBlock(context, activeTrip, messages, out var unavailableHints);
            var emptyConversation = messages.Count == 0;

            if (emptyConversation &&
                operation is WhatsAppAiAssistOperations.Suggest or WhatsAppAiAssistOperations.Regenerate or WhatsAppAiAssistOperations.Summary)
            {
                var emptyResult = new WhatsAppAiAssistResultDto(
                    "This conversation has no messages yet. Ask the customer how you can help, or wait for their first message.",
                    WhatsAppAiAssistConfidence.Low,
                    ReviewRecommended: true,
                    Provider: "none",
                    Model: null,
                    DurationMs: (int)sw.ElapsedMilliseconds,
                    UnavailableFacts: unavailableHints,
                    Operation: operation,
                    ConfidenceReason: "Empty conversation");
                await LogAsync(tenantId, conversationId, userId, operation, "none", null, sw, true, emptyResult.Confidence, null, cancellationToken);
                return ApiResponse<WhatsAppAiAssistResultDto>.SuccessResponse(emptyResult);
            }

            var (provider, config) = await providerResolver.ResolveAsync(tenantId, cancellationToken);
            providerName = config.Provider ?? provider?.ProviderName;
            model = string.IsNullOrWhiteSpace(config.ModelName) ? null : config.ModelName.Trim();

            if (provider is null)
            {
                await LogAsync(tenantId, conversationId, userId, operation, providerName, model, sw, false, null, "PROVIDER_UNAVAILABLE", cancellationToken);
                return ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                    "AI provider is not configured or available for this tenant.",
                    code: "PROVIDER_UNAVAILABLE");
            }

            var userPrompt = buildUserPrompt(facts);
            var chatRequest = new AiProviderChatRequest(
                Model: model ?? "mistral",
                Messages:
                [
                    new AiChatMessageDto("system", systemPrompt),
                    new AiChatMessageDto("user", userPrompt)
                ],
                BaseUrl: config.ApiEndpoint,
                Temperature: 0.2,
                MaxTokens: 1024);

            AiProviderChatResult llm;
            try
            {
                llm = await provider.ChatAsync(chatRequest, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await LogAsync(tenantId, conversationId, userId, operation, providerName, model, sw, false, null, "TIMEOUT", cancellationToken);
                return ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                    "AI provider timed out. Try again or draft the reply manually.",
                    code: "TIMEOUT");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WhatsApp AI Assist provider failure tenant={TenantId} conv={ConversationId}", tenantId, conversationId);
                await LogAsync(tenantId, conversationId, userId, operation, providerName, model, sw, false, null, "PROVIDER_ERROR", cancellationToken);
                return ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                    "AI provider failed. Try again or draft the reply manually.",
                    code: "PROVIDER_ERROR");
            }

            if (!llm.Success)
            {
                var code = "PROVIDER_ERROR";
                await LogAsync(tenantId, conversationId, userId, operation, llm.Provider, llm.Model, sw, false, null, code, cancellationToken);
                return ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                    string.IsNullOrWhiteSpace(llm.Error)
                        ? "AI provider failed. Try again or draft the reply manually."
                        : llm.Error!,
                    code: code);
            }

            providerName = llm.Provider;
            model = llm.Model;
            var parsed = WhatsAppAiAssistPrompt.ParseModelJson(llm.Content, unavailableHints, emptyConversation);
            var low = parsed.Confidence.Equals(WhatsAppAiAssistConfidence.Low, StringComparison.OrdinalIgnoreCase);
            var result = new WhatsAppAiAssistResultDto(
                parsed.Suggestion,
                parsed.Confidence,
                ReviewRecommended: low,
                Provider: llm.Provider,
                Model: llm.Model,
                DurationMs: (int)sw.ElapsedMilliseconds,
                UnavailableFacts: parsed.UnavailableFacts,
                Operation: operation,
                ConfidenceReason: low && string.IsNullOrWhiteSpace(parsed.ConfidenceReason)
                    ? "Human review recommended"
                    : parsed.ConfidenceReason);

            await LogAsync(tenantId, conversationId, userId, operation, llm.Provider, llm.Model, sw, true, result.Confidence, null, cancellationToken);

            try
            {
                var tokens = llm.PromptTokens + llm.CompletionTokens;
                await aiManagement.RecordUsageAsync(tenantId, "whatsapp_assist", llm.Provider, tokens, null, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "WhatsApp AI Assist usage ledger write failed");
            }

            return ApiResponse<WhatsAppAiAssistResultDto>.SuccessResponse(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "WhatsApp AI Assist unexpected error tenant={TenantId} conv={ConversationId}", tenantId, conversationId);
            await LogAsync(tenantId, conversationId, userId, operation, providerName, model, sw, false, null, "UNEXPECTED", cancellationToken);
            return ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                "AI assist failed unexpectedly.",
                code: "UNEXPECTED");
        }
    }

    private Task LogAsync(
        int tenantId,
        int conversationId,
        int? userId,
        string operation,
        string? provider,
        string? model,
        Stopwatch sw,
        bool success,
        string? confidence,
        string? errorCode,
        CancellationToken ct)
        => audit.InsertAsync(
            tenantId,
            conversationId,
            userId,
            operation,
            provider,
            model,
            (int)sw.ElapsedMilliseconds,
            success,
            confidence,
            errorCode,
            ct);
}
