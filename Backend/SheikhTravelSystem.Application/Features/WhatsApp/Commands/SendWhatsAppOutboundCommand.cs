using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record SendWhatsAppOutboundCommand(
    int? WhatsAppAccountId,
    int? ConversationId,
    string? RecipientPhoneNumber,
    string MessageType,
    string? Text)
    : IRequest<ApiResponse<SendWhatsAppMessageResultDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "WhatsAppMessage";
    public int? AuditEntityId => null;
}

public class SendWhatsAppOutboundCommandValidator : AbstractValidator<SendWhatsAppOutboundCommand>
{
    public SendWhatsAppOutboundCommandValidator()
    {
        RuleFor(x => x.MessageType).NotEmpty().MaximumLength(40);
        RuleFor(x => x)
            .Must(x => x.ConversationId is > 0 || !string.IsNullOrWhiteSpace(x.RecipientPhoneNumber))
            .WithMessage("ConversationId or RecipientPhoneNumber is required.");
        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(4096)
            .When(x => string.Equals(x.MessageType, "text", StringComparison.OrdinalIgnoreCase));
    }
}

public class SendWhatsAppOutboundCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppAccountConfig accountConfig,
    IWhatsAppAccountResolver accountResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IWhatsAppRealtimePublisher realtime)
    : IRequestHandler<SendWhatsAppOutboundCommand, ApiResponse<SendWhatsAppMessageResultDto>>
{
    public async Task<ApiResponse<SendWhatsAppMessageResultDto>> Handle(
        SendWhatsAppOutboundCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var messageType = string.IsNullOrWhiteSpace(request.MessageType) ? "text" : request.MessageType.Trim();
        if (!string.Equals(messageType, "text", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Only text messages are supported in this version.");

        string? recipient = request.RecipientPhoneNumber;
        int? conversationAccountId = null;
        WhatsAppConversationDto? conversation = null;

        if (request.ConversationId is > 0)
        {
            conversation = await repository.GetConversationAsync(tenantId, request.ConversationId.Value, cancellationToken);
            if (conversation is null)
                return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Conversation not found.");
            conversationAccountId = conversation.AccountId;
            if (string.IsNullOrWhiteSpace(recipient))
                recipient = conversation.ContactPhone;
        }

        if (string.IsNullOrWhiteSpace(recipient))
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Recipient phone number is required.");

        // Explicit account id wins; else conversation account; else phone routing.
        WhatsAppAccountRow? account;
        if (request.WhatsAppAccountId is > 0)
        {
            account = await accountResolver.ResolveAsync(
                tenantId, request.WhatsAppAccountId, recipient, cancellationToken);
        }
        else if (conversationAccountId is > 0)
        {
            account = await repository.GetAccountByIdAsync(tenantId, conversationAccountId.Value, cancellationToken);
            if (account is { IsActive: false })
                account = null;
        }
        else
        {
            account = await accountResolver.ResolveAsync(tenantId, null, recipient, cancellationToken);
        }

        if (account is null)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("WhatsApp account not found or inactive.");

        if (accountConfig.ResolveCredentials(account) is null)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(
                "WhatsApp credentials are not configured for this account.");

        var conversationId = conversation?.Id
            ?? await repository.EnsureConversationAsync(
                tenantId, account.Id, WhatsAppPhone.ToE164(recipient), null, cancellationToken);

        if (conversation is null)
            conversation = await repository.GetConversationAsync(tenantId, conversationId, cancellationToken);

        // Meta CS window: free-form text only within 24h of last inbound customer message.
        if (!WhatsAppMessagingWindow.IsOpenFromStoredWindow(
                conversation?.LastIncomingMessageAt, conversation?.WindowExpiresAt))
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(
                "Messaging window closed. Send an approved template instead.",
                code: "WINDOW_CLOSED");

        // Human agent take-over: disable bot and assign current user when unset.
        var agentId = currentUser.UserId;
        if (agentId is > 0)
        {
            await repository.SetConversationBotAsync(
                tenantId, conversationId, isBotEnabled: false,
                currentBotState: WhatsAppDemoBotStates.HandedOff, cancellationToken);
            if (conversation?.AssignedUserId is null)
            {
                await repository.SetConversationAssignmentAsync(
                    tenantId, conversationId, agentId, cancellationToken);
            }
        }

        var text = request.Text!.Trim();
        var to = WhatsAppPhone.ToApiDigits(recipient);

        var pendingId = await repository.InsertOutboundMessageAsync(
            tenantId, conversationId, null, messageType, text, "Queued", cancellationToken);
        await repository.SetOutboundStatusAsync(pendingId, "Sending", null, cancellationToken);

        var send = await cloudApi.SendTextAsync(account, to, text, cancellationToken);
        if (!send.Success)
        {
            await repository.SetOutboundMetaIdAsync(
                pendingId, send.MetaMessageId ?? $"fail-{pendingId}", "Failed", cancellationToken);
            await repository.SetOutboundStatusAsync(pendingId, "Failed", send.ErrorMessage, cancellationToken);
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(send.ErrorMessage ?? "Send failed.");
        }

        await repository.SetOutboundMetaIdAsync(pendingId, send.MetaMessageId!, "Sent", cancellationToken);

        await realtime.PublishAsync(tenantId, new
        {
            type = "whatsapp.message",
            accountCode = account.Code,
            conversationId,
            messageId = pendingId,
            direction = "Outbound",
            unreadCount = 0
        }, cancellationToken);

        return ApiResponse<SendWhatsAppMessageResultDto>.SuccessResponse(
            new SendWhatsAppMessageResultDto(pendingId, send.MetaMessageId, "Sent"),
            "Message sent.");
    }
}
