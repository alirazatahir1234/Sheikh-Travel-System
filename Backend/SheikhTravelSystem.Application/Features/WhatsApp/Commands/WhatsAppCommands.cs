using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record IngestWhatsAppWebhookCommand(string Payload) : IRequest<ApiResponse<object>>;

public class IngestWhatsAppWebhookCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppAccountConfig accountConfig,
    IWhatsAppRealtimePublisher realtime,
    IWhatsAppBotOrchestrator botOrchestrator,
    IOptions<WhatsAppOptions> options,
    ILogger<IngestWhatsAppWebhookCommandHandler> logger)
    : IRequestHandler<IngestWhatsAppWebhookCommand, ApiResponse<object>>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "sent", "delivered", "read", "failed"
    };

    public async Task<ApiResponse<object>> Handle(IngestWhatsAppWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return ApiResponse<object>.SuccessResponse(new { ignored = true, reason = "disabled" });

        try
        {
            using var doc = JsonDocument.Parse(request.Payload);
            var root = doc.RootElement;
            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return ApiResponse<object>.SuccessResponse(new { ok = true });

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                        continue;

                    var phoneNumberId = value.TryGetProperty("metadata", out var meta)
                        && meta.TryGetProperty("phone_number_id", out var pn)
                        ? pn.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(phoneNumberId))
                        continue;

                    var accountRows = await repository.ListActiveAccountRowsAsync(cancellationToken);
                    var account = accountConfig.ResolveAccountByPhoneNumberId(phoneNumberId, accountRows);
                    if (account is null)
                    {
                        logger.LogWarning("WhatsApp webhook for unknown phone_number_id={PhoneNumberId}", phoneNumberId);
                        await repository.InsertWebhookDeadLetterAsync(
                            $"Unknown phone_number_id={phoneNumberId}",
                            request.Payload.Length > 500 ? request.Payload[..500] : request.Payload,
                            cancellationToken);
                        continue;
                    }

                    if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var st in statuses.EnumerateArray())
                        {
                            var id = st.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                            var status = st.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
                            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status))
                                continue;
                            if (!AllowedStatuses.Contains(status))
                                continue;

                            string? errorMessage = null;
                            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                                && st.TryGetProperty("errors", out var errors)
                                && errors.ValueKind == JsonValueKind.Array
                                && errors.GetArrayLength() > 0)
                            {
                                var e0 = errors[0];
                                errorMessage = e0.TryGetProperty("title", out var title) ? title.GetString()
                                    : e0.TryGetProperty("message", out var msg) ? msg.GetString() : null;
                            }

                            var updated = await repository.UpdateMessageStatusAsync(id, status, errorMessage, cancellationToken);
                            if (updated is not null)
                            {
                                await realtime.PublishAsync(updated.TenantId, new
                                {
                                    type = "whatsapp.message_status",
                                    conversationId = updated.ConversationId,
                                    messageId = updated.MessageDbId,
                                    metaMessageId = updated.MetaMessageId,
                                    status = updated.Status
                                }, cancellationToken);
                            }
                        }
                    }

                    if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                    {
                        string? contactName = null;
                        string? waId = null;
                        if (value.TryGetProperty("contacts", out var contacts)
                            && contacts.ValueKind == JsonValueKind.Array
                            && contacts.GetArrayLength() > 0)
                        {
                            var c0 = contacts[0];
                            waId = c0.TryGetProperty("wa_id", out var wa) ? wa.GetString() : null;
                            if (c0.TryGetProperty("profile", out var profile)
                                && profile.TryGetProperty("name", out var nameEl))
                                contactName = nameEl.GetString();
                        }

                        foreach (var msg in messages.EnumerateArray())
                        {
                            var metaId = msg.TryGetProperty("id", out var mid) ? mid.GetString() : null;
                            if (string.IsNullOrWhiteSpace(metaId)) continue;
                            if (await repository.MessageExistsAsync(metaId, cancellationToken))
                                continue;

                            var from = msg.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : waId;
                            if (string.IsNullOrWhiteSpace(from)) continue;

                            var type = msg.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "unknown" : "unknown";
                            string? body = null;
                            string? mediaId = null;
                            if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase)
                                && msg.TryGetProperty("text", out var text)
                                && text.TryGetProperty("body", out var bodyEl))
                            {
                                body = bodyEl.GetString();
                            }
                            else if (string.Equals(type, "interactive", StringComparison.OrdinalIgnoreCase)
                                     && msg.TryGetProperty("interactive", out var interactive))
                            {
                                body = ExtractInteractiveReply(interactive) ?? "[interactive]";
                            }
                            else if (msg.TryGetProperty(type, out var mediaObj)
                                     && mediaObj.ValueKind == JsonValueKind.Object)
                            {
                                if (mediaObj.TryGetProperty("id", out var mediaIdEl))
                                    mediaId = mediaIdEl.GetString();
                                body = mediaObj.TryGetProperty("caption", out var cap)
                                    ? cap.GetString()
                                    : $"[{type}]";
                            }
                            else
                            {
                                body = $"[{type}]";
                            }

                            var phoneE164 = WhatsAppPhone.ToE164(from);
                            var messageId = await repository.UpsertInboundMessageAsync(new WhatsAppInboundPersistRequest(
                                account.TenantId,
                                account.Id,
                                from,
                                phoneE164,
                                contactName,
                                metaId,
                                type,
                                body,
                                mediaId,
                                msg.GetRawText()), cancellationToken);

                            if (messageId <= 0)
                                continue;

                            var (items, _) = await repository.GetConversationsAsync(
                                account.TenantId, account.Id, phoneE164, null, null, 1, 1, cancellationToken);
                            var top = items.FirstOrDefault();
                            await realtime.PublishAsync(account.TenantId, new
                            {
                                type = "whatsapp.message",
                                accountCode = account.Code,
                                conversationId = top?.Id,
                                messageId,
                                direction = "Inbound",
                                unreadCount = top?.UnreadCount
                            }, cancellationToken);
                            if (top is not null)
                            {
                                await realtime.PublishAsync(account.TenantId, new
                                {
                                    type = "whatsapp.unread_count_changed",
                                    conversationId = top.Id,
                                    unreadCount = top.UnreadCount
                                }, cancellationToken);

                                try
                                {
                                    await botOrchestrator.HandleInboundAsync(
                                        account.TenantId,
                                        account,
                                        top,
                                        phoneE164,
                                        contactName,
                                        body,
                                        type,
                                        cancellationToken);
                                }
                                catch (Exception botEx) when (botEx is not OperationCanceledException)
                                {
                                    logger.LogWarning(botEx, "WhatsApp bot orchestrator failed for conversation {Id}", top.Id);
                                }
                            }
                        }
                    }
                }
            }

            return ApiResponse<object>.SuccessResponse(new { ok = true });
        }
        catch (JsonException)
        {
            return ApiResponse<object>.SuccessResponse(new { ok = true, ignored = "invalid_json" });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "WhatsApp webhook ingest failed");
            await repository.InsertWebhookDeadLetterAsync(
                ex.Message,
                request.Payload.Length > 500 ? request.Payload[..500] : request.Payload,
                cancellationToken);
            // Still report success to the controller so Meta receives HTTP 200.
            return ApiResponse<object>.SuccessResponse(new { ok = true, deadLetter = true });
        }
    }

    private static string? ExtractInteractiveReply(JsonElement interactive)
    {
        if (interactive.TryGetProperty("list_reply", out var listReply))
        {
            if (listReply.TryGetProperty("title", out var title) && !string.IsNullOrWhiteSpace(title.GetString()))
                return title.GetString();
            if (listReply.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.GetString()))
                return id.GetString();
        }

        if (interactive.TryGetProperty("button_reply", out var buttonReply))
        {
            if (buttonReply.TryGetProperty("title", out var title) && !string.IsNullOrWhiteSpace(title.GetString()))
                return title.GetString();
            if (buttonReply.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.GetString()))
                return id.GetString();
        }

        return null;
    }
}

public record SendWhatsAppMessageCommand(int ConversationId, string Body)
    : IRequest<ApiResponse<SendWhatsAppMessageResultDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "WhatsAppMessage";
    public int? AuditEntityId => null;
}

public class SendWhatsAppMessageCommandValidator : AbstractValidator<SendWhatsAppMessageCommand>
{
    public SendWhatsAppMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId).GreaterThan(0);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4096);
    }
}

public class SendWhatsAppMessageCommandHandler(
    IMediator mediator)
    : IRequestHandler<SendWhatsAppMessageCommand, ApiResponse<SendWhatsAppMessageResultDto>>
{
    public Task<ApiResponse<SendWhatsAppMessageResultDto>> Handle(
        SendWhatsAppMessageCommand request, CancellationToken cancellationToken)
        => mediator.Send(new SendWhatsAppOutboundCommand(
            WhatsAppAccountId: null,
            ConversationId: request.ConversationId,
            RecipientPhoneNumber: null,
            MessageType: "text",
            Text: request.Body), cancellationToken);
}

public record MarkWhatsAppConversationReadCommand(int ConversationId)
    : IRequest<ApiResponse<object>>;

public class MarkWhatsAppConversationReadCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppRealtimePublisher realtime,
    ITenantContext tenantContext)
    : IRequestHandler<MarkWhatsAppConversationReadCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(MarkWhatsAppConversationReadCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var conversation = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (conversation is null)
            return ApiResponse<object>.FailResponse("Conversation not found.");
        await repository.MarkConversationReadAsync(tenantId, request.ConversationId, cancellationToken);
        var updated = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (updated is not null)
        {
            await SetWhatsAppConversationAssignmentCommandHandler.PublishConversationUpdatedAsync(
                realtime, tenantId, updated, cancellationToken);
            await realtime.PublishAsync(tenantId, new
            {
                type = "whatsapp.unread_count_changed",
                conversationId = updated.Id,
                unreadCount = 0
            }, cancellationToken);
        }

        return ApiResponse<object>.SuccessResponse(new { ok = true });
    }
}

public record LinkWhatsAppContactCustomerCommand(int ContactId, int? CustomerId)
    : IRequest<ApiResponse<WhatsAppContactDto>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "WhatsAppContact";
    public int? AuditEntityId => ContactId;
}

public class LinkWhatsAppContactCustomerCommandHandler(
    IWhatsAppInboxRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<LinkWhatsAppContactCustomerCommand, ApiResponse<WhatsAppContactDto>>
{
    public async Task<ApiResponse<WhatsAppContactDto>> Handle(
        LinkWhatsAppContactCustomerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var contact = await repository.GetContactAsync(tenantId, request.ContactId, cancellationToken);
        if (contact is null)
            return ApiResponse<WhatsAppContactDto>.FailResponse("Contact not found.");

        await repository.LinkCustomerAsync(tenantId, request.ContactId, request.CustomerId, cancellationToken);
        var updated = await repository.GetContactAsync(tenantId, request.ContactId, cancellationToken);
        return ApiResponse<WhatsAppContactDto>.SuccessResponse(updated!, "Customer linked.");
    }
}

public record AutoLinkWhatsAppContactCommand(int ContactId)
    : IRequest<ApiResponse<WhatsAppContactDto>>;

public class AutoLinkWhatsAppContactCommandHandler(
    IWhatsAppInboxRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<AutoLinkWhatsAppContactCommand, ApiResponse<WhatsAppContactDto>>
{
    public async Task<ApiResponse<WhatsAppContactDto>> Handle(
        AutoLinkWhatsAppContactCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var contact = await repository.GetContactAsync(tenantId, request.ContactId, cancellationToken);
        if (contact is null)
            return ApiResponse<WhatsAppContactDto>.FailResponse("Contact not found.");

        var customerId = await repository.FindCustomerIdByPhoneAsync(tenantId, contact.PhoneE164, cancellationToken);
        if (customerId is null)
            return ApiResponse<WhatsAppContactDto>.FailResponse("No matching customer phone found.");

        await repository.LinkCustomerAsync(tenantId, request.ContactId, customerId, cancellationToken);
        var updated = await repository.GetContactAsync(tenantId, request.ContactId, cancellationToken);
        return ApiResponse<WhatsAppContactDto>.SuccessResponse(updated!, "Customer matched by phone.");
    }
}
