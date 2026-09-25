using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public interface IWhatsAppBotOrchestrator
{
    /// <summary>
    /// After inbound persist: CRM link + DEMO bot when enabled.
    /// </summary>
    Task HandleInboundAsync(
        int tenantId,
        WhatsAppAccountRow account,
        WhatsAppConversationDto conversation,
        string phoneE164,
        string? profileName,
        string? inboundText,
        string? inboundType,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppBotOrchestrator(
    IWhatsAppCrmLeadService crmLeadService,
    IWhatsAppDemoQualificationBot demoBot,
    IWhatsAppInboxRepository repository,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppAccountConfig accountConfig,
    IWhatsAppRealtimePublisher realtime,
    ILogger<WhatsAppBotOrchestrator> logger) : IWhatsAppBotOrchestrator
{
    public async Task HandleInboundAsync(
        int tenantId,
        WhatsAppAccountRow account,
        WhatsAppConversationDto conversation,
        string phoneE164,
        string? profileName,
        string? inboundText,
        string? inboundType,
        CancellationToken cancellationToken = default)
    {
        await crmLeadService.EnsureLeadLinkedAsync(
            tenantId, conversation, phoneE164, profileName, cancellationToken);

        // Reload after CRM link so LeadId is current.
        var refreshed = await repository.GetConversationAsync(tenantId, conversation.Id, cancellationToken)
                        ?? conversation;

        if (!refreshed.IsBotEnabled)
            return;

        var userText = ExtractUserText(inboundText, inboundType);
        var reply = demoBot.Process(new WhatsAppDemoBotInput(
            refreshed.CurrentBotState ?? WhatsAppDemoBotStates.Idle,
            refreshed.IsBotEnabled,
            userText));

        if (reply is null)
            return;

        if (reply.StoredFleetType is not null
            || reply.StoredFleetSize is not null
            || reply.StoredChallenge is not null
            || reply.QualifyLead)
        {
            if (refreshed.LeadId is > 0)
            {
                var message = reply.QualifyLead
                    ? BuildQualificationSummary(reply)
                    : null;
                await repository.UpdateWhatsAppLeadQualificationAsync(new WhatsAppLeadQualificationUpdate(
                    tenantId,
                    refreshed.LeadId.Value,
                    reply.StoredFleetType,
                    reply.StoredFleetSize,
                    reply.StoredChallenge,
                    reply.QualifyLead ? "Qualified" : null,
                    message), cancellationToken);
            }
        }

        await repository.SetConversationBotAsync(
            tenantId,
            refreshed.Id,
            isBotEnabled: true,
            currentBotState: reply.NextState,
            cancellationToken);

        await SendBotReplyAsync(tenantId, account, refreshed, phoneE164, reply, cancellationToken);
    }

    private async Task SendBotReplyAsync(
        int tenantId,
        WhatsAppAccountRow account,
        WhatsAppConversationDto conversation,
        string phoneE164,
        WhatsAppDemoBotReply reply,
        CancellationToken ct)
    {
        if (accountConfig.ResolveCredentials(account) is null)
        {
            logger.LogWarning("Bot reply skipped — credentials missing for account {Code}", account.Code);
            return;
        }

        var to = WhatsAppPhone.ToApiDigits(phoneE164);
        var pendingId = await repository.InsertOutboundMessageAsync(
            tenantId, conversation.Id, null, "text", reply.TextBody, "Queued", ct);
        await repository.SetOutboundStatusAsync(pendingId, "Sending", null, ct);

        WhatsAppCloudApiResult send;
        if (reply.ListOptions is { Count: > 0 })
        {
            send = await cloudApi.SendInteractiveListAsync(
                account,
                to,
                bodyText: reply.TextBody.Split('\n')[0],
                buttonLabel: reply.ListButtonLabel ?? "Options",
                sectionTitle: reply.ListButtonLabel ?? "Choose",
                options: reply.ListOptions,
                cancellationToken: ct);

            if (!send.Success)
            {
                // Fallback to numbered text (still deterministic).
                send = await cloudApi.SendTextAsync(account, to, reply.TextBody, ct);
            }
        }
        else
        {
            send = await cloudApi.SendTextAsync(account, to, reply.TextBody, ct);
        }

        if (!send.Success)
        {
            await repository.SetOutboundMetaIdAsync(
                pendingId, send.MetaMessageId ?? $"fail-{pendingId}", "Failed", ct);
            await repository.SetOutboundStatusAsync(pendingId, "Failed", send.ErrorMessage, ct);
            logger.LogWarning("Bot outbound failed: {Error}", send.ErrorMessage);
            return;
        }

        await repository.SetOutboundMetaIdAsync(pendingId, send.MetaMessageId!, "Sent", ct);
        await realtime.PublishAsync(tenantId, new
        {
            type = "whatsapp.message",
            accountCode = account.Code,
            conversationId = conversation.Id,
            messageId = pendingId,
            direction = "Outbound",
            unreadCount = conversation.UnreadCount
        }, ct);
    }

    private static string? ExtractUserText(string? body, string? type)
    {
        if (!string.IsNullOrWhiteSpace(body))
            return body.Trim();
        return null;
    }

    private static string BuildQualificationSummary(WhatsAppDemoBotReply reply)
    {
        var parts = new List<string> { "WhatsApp DEMO qualification" };
        if (!string.IsNullOrWhiteSpace(reply.StoredFleetType))
            parts.Add($"Fleet: {reply.StoredFleetType}");
        if (!string.IsNullOrWhiteSpace(reply.StoredFleetSize))
            parts.Add($"Size: {reply.StoredFleetSize}");
        if (!string.IsNullOrWhiteSpace(reply.StoredChallenge))
            parts.Add($"Challenge: {reply.StoredChallenge}");
        return string.Join(". ", parts) + ".";
    }
}
