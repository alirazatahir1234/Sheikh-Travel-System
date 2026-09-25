using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record GetWhatsAppWebhookLogsQuery(int Page = 1, int PageSize = 30, string? Status = null)
    : IRequest<ApiResponse<object>>;

public class GetWhatsAppWebhookLogsQueryHandler(IWhatsAppInboxExtended repository)
    : IRequestHandler<GetWhatsAppWebhookLogsQuery, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(GetWhatsAppWebhookLogsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await repository.GetWebhookLogsAsync(
            request.Page, request.PageSize, request.Status, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { items, total, page = request.Page, pageSize = request.PageSize });
    }
}

public record RequeueWhatsAppWebhookLogCommand(long Id) : IRequest<ApiResponse<object>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "WhatsAppWebhookLog";
    public int? AuditEntityId => null;
}

public class RequeueWhatsAppWebhookLogCommandHandler(
    IWhatsAppInboxExtended repository,
    IWhatsAppWorkQueue workQueue)
    : IRequestHandler<RequeueWhatsAppWebhookLogCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        RequeueWhatsAppWebhookLogCommand request, CancellationToken cancellationToken)
    {
        var log = await repository.GetWebhookLogAsync(request.Id, cancellationToken);
        if (log is null)
            return ApiResponse<object>.FailResponse("Webhook log not found.");
        if (!log.SignatureValid)
            return ApiResponse<object>.FailResponse("Cannot requeue a log with invalid signature.");

        var payload = await repository.GetWebhookLogPayloadAsync(request.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return ApiResponse<object>.FailResponse("Webhook payload missing.");

        var newId = await repository.InsertWebhookLogAsync(
            signatureValid: true,
            payload: payload,
            processingStatus: "Received",
            phoneNumberId: log.PhoneNumberId,
            tenantId: log.TenantId,
            ct: cancellationToken);

        await workQueue.EnqueueInboundAsync(
            new WhatsAppInboundWorkItem(newId, payload, SignatureValid: true),
            cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { requeued = true, webhookLogId = newId });
    }
}

public record RetryWhatsAppMessageCommand(int MessageId)
    : IRequest<ApiResponse<SendWhatsAppMessageResultDto>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "WhatsAppMessage";
    public int? AuditEntityId => MessageId;
}

public class RetryWhatsAppMessageCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppInboxExtended inboxExtended,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppAccountConfig accountConfig,
    ITenantContext tenantContext,
    IWhatsAppRealtimePublisher realtime)
    : IRequestHandler<RetryWhatsAppMessageCommand, ApiResponse<SendWhatsAppMessageResultDto>>
{
    public const int MaxAttempts = 3;

    public async Task<ApiResponse<SendWhatsAppMessageResultDto>> Handle(
        RetryWhatsAppMessageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await inboxExtended.GetOutboundMessageForRetryAsync(tenantId, request.MessageId, cancellationToken);
        if (row is null)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Message not found.");

        if (!string.Equals(row.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Only failed messages can be retried.");

        if (row.AttemptCount >= MaxAttempts)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(
                $"Retry limit reached ({MaxAttempts}).");

        var conversation = await repository.GetConversationAsync(tenantId, row.ConversationId, cancellationToken);
        if (conversation is null)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Conversation not found.");

        var account = await repository.GetAccountByIdAsync(tenantId, row.AccountId, cancellationToken);
        if (account is null || !account.IsActive)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("WhatsApp account not found or inactive.");
        if (accountConfig.ResolveCredentials(account) is null)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("WhatsApp credentials are not configured.");

        var isTemplate = !string.IsNullOrWhiteSpace(row.TemplateName)
            || string.Equals(row.MessageType, "template", StringComparison.OrdinalIgnoreCase);

        if (!isTemplate
            && !WhatsAppMessagingWindow.IsOpenFromStoredWindow(
                conversation.LastIncomingMessageAt, conversation.WindowExpiresAt))
        {
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(
                "Messaging window closed. Send an approved template instead.",
                code: "WINDOW_CLOSED");
        }

        WhatsAppCloudApiResult send;
        if (isTemplate && !string.IsNullOrWhiteSpace(row.TemplateName))
        {
            send = await cloudApi.SendTemplateAsync(
                account, conversation.ContactPhone, row.TemplateName, "en",
                bodyParameters: null, cancellationToken: cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(row.Text))
                return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Message body is empty.");
            send = await cloudApi.SendTextAsync(account, conversation.ContactPhone, row.Text, cancellationToken);
        }

        if (!send.Success)
        {
            await inboxExtended.IncrementMessageAttemptAsync(
                tenantId, row.Id, null, "Failed", cancellationToken);
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(
                send.ErrorMessage ?? "Retry send failed.");
        }

        await inboxExtended.IncrementMessageAttemptAsync(
            tenantId, row.Id, send.MetaMessageId, "Sent", cancellationToken);

        await realtime.PublishAsync(tenantId, new
        {
            type = "whatsapp.message_status",
            conversationId = row.ConversationId,
            messageId = row.Id,
            metaMessageId = send.MetaMessageId,
            status = "Sent"
        }, cancellationToken);

        return ApiResponse<SendWhatsAppMessageResultDto>.SuccessResponse(
            new SendWhatsAppMessageResultDto(row.Id, send.MetaMessageId, "Sent"));
    }
}
