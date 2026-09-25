using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public interface IWhatsAppBotOrchestrator
{
    /// <summary>
    /// After inbound persist: CRM link + DEMO or self-service bot when enabled.
    /// </summary>
    Task HandleInboundAsync(
        int tenantId,
        WhatsAppAccountRow account,
        WhatsAppConversationDto conversation,
        string phoneE164,
        string? profileName,
        string? inboundText,
        string? inboundType,
        CancellationToken cancellationToken = default,
        string? buttonPayload = null,
        string? flowIdempotencyKey = null,
        string? flowPayloadJson = null);
}

public sealed class WhatsAppBotOrchestrator(
    IWhatsAppCrmLeadService crmLeadService,
    IWhatsAppDemoQualificationBot demoBot,
    IWhatsAppSelfServiceBot selfServiceBot,
    IWhatsAppSelfServiceRepository selfServiceRepository,
    IWhatsAppInboxRepository repository,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppCloudApiExtended cloudApiExtended,
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
        CancellationToken cancellationToken = default,
        string? buttonPayload = null,
        string? flowIdempotencyKey = null,
        string? flowPayloadJson = null)
    {
        await crmLeadService.EnsureLeadLinkedAsync(
            tenantId, conversation, phoneE164, profileName, cancellationToken);

        var refreshed = await repository.GetConversationAsync(tenantId, conversation.Id, cancellationToken)
                        ?? conversation;

        if (!refreshed.IsBotEnabled)
            return;

        var userText = ExtractUserText(inboundText, inboundType);
        var state = refreshed.CurrentBotState ?? WhatsAppDemoBotStates.Idle;

        // DEMO path when keyword or mid-demo FSM
        var isDemoKeyword = string.Equals(userText?.Trim(), "DEMO", StringComparison.OrdinalIgnoreCase);
        if (isDemoKeyword || WhatsAppSelfServiceStates.IsDemoState(state))
        {
            var demoReply = demoBot.Process(new WhatsAppDemoBotInput(
                state,
                refreshed.IsBotEnabled,
                userText));

            if (demoReply is null)
                return;

            if (demoReply.StoredFleetType is not null
                || demoReply.StoredFleetSize is not null
                || demoReply.StoredChallenge is not null
                || demoReply.QualifyLead)
            {
                if (refreshed.LeadId is > 0)
                {
                    var message = demoReply.QualifyLead
                        ? BuildQualificationSummary(demoReply)
                        : null;
                    await repository.UpdateWhatsAppLeadQualificationAsync(new WhatsAppLeadQualificationUpdate(
                        tenantId,
                        refreshed.LeadId.Value,
                        demoReply.StoredFleetType,
                        demoReply.StoredFleetSize,
                        demoReply.StoredChallenge,
                        demoReply.QualifyLead ? "Qualified" : null,
                        message), cancellationToken);
                }
            }

            await repository.SetConversationBotAsync(
                tenantId,
                refreshed.Id,
                isBotEnabled: true,
                currentBotState: demoReply.NextState,
                cancellationToken);

            await SendDemoReplyAsync(tenantId, account, refreshed, phoneE164, demoReply, cancellationToken);
            return;
        }

        var ssReply = await selfServiceBot.ProcessAsync(new WhatsAppSelfServiceContext(
            tenantId,
            refreshed.Id,
            phoneE164,
            profileName,
            state,
            refreshed.IsBotEnabled,
            userText,
            buttonPayload,
            flowIdempotencyKey,
            flowPayloadJson), cancellationToken);

        if (ssReply is null)
            return;

        if (ssReply.ClearSession)
            await selfServiceRepository.SetBotSessionJsonAsync(tenantId, refreshed.Id, null, cancellationToken);
        else if (ssReply.Session is not null)
            await selfServiceRepository.SetBotSessionJsonAsync(
                tenantId, refreshed.Id, ssReply.Session.ToJson(), cancellationToken);

        await repository.SetConversationBotAsync(
            tenantId,
            refreshed.Id,
            isBotEnabled: !ssReply.DisableBot,
            currentBotState: ssReply.DisableBot
                ? WhatsAppSelfServiceStates.HumanHandoff
                : ssReply.NextState,
            cancellationToken);

        await SendSelfServiceReplyAsync(tenantId, account, refreshed, phoneE164, ssReply, cancellationToken);
    }

    private async Task SendDemoReplyAsync(
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
                send = await cloudApi.SendTextAsync(account, to, reply.TextBody, ct);
        }
        else
        {
            send = await cloudApi.SendTextAsync(account, to, reply.TextBody, ct);
        }

        await FinalizeOutboundAsync(tenantId, account, conversation, pendingId, send, ct);
    }

    private async Task SendSelfServiceReplyAsync(
        int tenantId,
        WhatsAppAccountRow account,
        WhatsAppConversationDto conversation,
        string phoneE164,
        WhatsAppSelfServiceReply reply,
        CancellationToken ct)
    {
        if (accountConfig.ResolveCredentials(account) is null)
        {
            logger.LogWarning("Self-service reply skipped — credentials missing for account {Code}", account.Code);
            return;
        }

        var to = WhatsAppPhone.ToApiDigits(phoneE164);
        var pendingId = await repository.InsertOutboundMessageAsync(
            tenantId, conversation.Id, null, "text", reply.TextBody, "Queued", ct);
        await repository.SetOutboundStatusAsync(pendingId, "Sending", null, ct);

        WhatsAppCloudApiResult send;
        var bodyPreview = FirstLine(reply.TextBody);

        if (reply.ListRows is { Count: > 0 })
        {
            send = await cloudApiExtended.SendInteractiveListRowsAsync(
                account, to, bodyPreview,
                reply.ListButtonLabel ?? "Menu",
                reply.ListButtonLabel ?? "Options",
                reply.ListRows, ct);
            if (!send.Success)
                send = await cloudApi.SendTextAsync(account, to, reply.TextBody, ct);
        }
        else if (reply.ReplyButtons is { Count: > 0 })
        {
            send = await cloudApiExtended.SendInteractiveReplyButtonsAsync(
                account, to, reply.TextBody, reply.ReplyButtons, ct);
            if (!send.Success)
                send = await cloudApi.SendTextAsync(account, to, reply.TextBody, ct);
        }
        else
        {
            send = await cloudApi.SendTextAsync(account, to, reply.TextBody, ct);
        }

        await FinalizeOutboundAsync(tenantId, account, conversation, pendingId, send, ct);
    }

    private async Task FinalizeOutboundAsync(
        int tenantId,
        WhatsAppAccountRow account,
        WhatsAppConversationDto conversation,
        int pendingId,
        WhatsAppCloudApiResult send,
        CancellationToken ct)
    {
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

    private static string FirstLine(string body)
    {
        var i = body.IndexOf('\n');
        return i < 0 ? body : body[..i];
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
