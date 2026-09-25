using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record SetWhatsAppConversationAssignmentCommand(int ConversationId, int? AssignedUserId)
    : IRequest<ApiResponse<WhatsAppConversationDto>>;

public class SetWhatsAppConversationAssignmentCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppRealtimePublisher realtime,
    ITenantContext tenantContext)
    : IRequestHandler<SetWhatsAppConversationAssignmentCommand, ApiResponse<WhatsAppConversationDto>>
{
    public async Task<ApiResponse<WhatsAppConversationDto>> Handle(
        SetWhatsAppConversationAssignmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (existing is null)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found.");

        var ok = await repository.SetConversationAssignmentAsync(
            tenantId, request.ConversationId, request.AssignedUserId, cancellationToken);
        if (!ok)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found.");

        // Human handoff: assigning an agent disables the bot.
        if (request.AssignedUserId is > 0)
        {
            await repository.SetConversationBotAsync(
                tenantId, request.ConversationId, isBotEnabled: false,
                currentBotState: WhatsAppDemoBotStates.HandedOff, cancellationToken);
        }

        var updated = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (updated is null)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found after update.");

        await PublishConversationUpdatedAsync(realtime, tenantId, updated, cancellationToken);
        return ApiResponse<WhatsAppConversationDto>.SuccessResponse(
            WhatsAppMessagingWindow.WithWindowFlag(updated));
    }

    internal static Task PublishConversationUpdatedAsync(
        IWhatsAppRealtimePublisher realtime,
        int tenantId,
        WhatsAppConversationDto conversation,
        CancellationToken ct)
        => realtime.PublishAsync(tenantId, new
        {
            type = "whatsapp.conversation_updated",
            conversationId = conversation.Id,
            accountCode = conversation.AccountCode,
            unreadCount = conversation.UnreadCount,
            status = conversation.Status,
            assignedUserId = conversation.AssignedUserId,
            assignedUserName = conversation.AssignedUserName,
            isBotEnabled = conversation.IsBotEnabled,
            currentBotState = conversation.CurrentBotState
        }, ct);
}

public record SetWhatsAppConversationStatusCommand(int ConversationId, string Status)
    : IRequest<ApiResponse<WhatsAppConversationDto>>;

public class SetWhatsAppConversationStatusCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppRealtimePublisher realtime,
    ITenantContext tenantContext)
    : IRequestHandler<SetWhatsAppConversationStatusCommand, ApiResponse<WhatsAppConversationDto>>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Open", "Pending", "Resolved"
    };

    public async Task<ApiResponse<WhatsAppConversationDto>> Handle(
        SetWhatsAppConversationStatusCommand request, CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim() ?? "";
        if (!Allowed.Contains(status))
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Status must be Open, Pending, or Resolved.");

        var normalized = Allowed.First(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (existing is null)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found.");

        var ok = await repository.SetConversationStatusAsync(
            tenantId, request.ConversationId, normalized, cancellationToken);
        if (!ok)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found.");

        var updated = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (updated is null)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found after update.");

        await SetWhatsAppConversationAssignmentCommandHandler.PublishConversationUpdatedAsync(
            realtime, tenantId, updated, cancellationToken);
        return ApiResponse<WhatsAppConversationDto>.SuccessResponse(
            WhatsAppMessagingWindow.WithWindowFlag(updated));
    }
}

public record SetWhatsAppConversationBotCommand(
    int ConversationId,
    bool IsBotEnabled,
    string? CurrentBotState = null)
    : IRequest<ApiResponse<WhatsAppConversationDto>>;

public class SetWhatsAppConversationBotCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppRealtimePublisher realtime,
    ITenantContext tenantContext)
    : IRequestHandler<SetWhatsAppConversationBotCommand, ApiResponse<WhatsAppConversationDto>>
{
    public async Task<ApiResponse<WhatsAppConversationDto>> Handle(
        SetWhatsAppConversationBotCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (existing is null)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found.");

        var state = request.IsBotEnabled
            ? (string.IsNullOrWhiteSpace(request.CurrentBotState)
                ? WhatsAppDemoBotStates.Idle
                : request.CurrentBotState.Trim())
            : null;

        var ok = await repository.SetConversationBotAsync(
            tenantId, request.ConversationId, request.IsBotEnabled, state, cancellationToken);
        if (!ok)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found.");

        var updated = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (updated is null)
            return ApiResponse<WhatsAppConversationDto>.FailResponse("Conversation not found after update.");

        await SetWhatsAppConversationAssignmentCommandHandler.PublishConversationUpdatedAsync(
            realtime, tenantId, updated, cancellationToken);
        return ApiResponse<WhatsAppConversationDto>.SuccessResponse(
            WhatsAppMessagingWindow.WithWindowFlag(updated));
    }
}

public record CreateWhatsAppConversationLeadCommand(int ConversationId)
    : IRequest<ApiResponse<WhatsAppConversationContextDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "WebsiteContactRequest";
    public int? AuditEntityId => null;
}

public class CreateWhatsAppConversationLeadCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppInboxExtended inboxExtended,
    IWhatsAppCrmLeadService crmLeadService,
    ITenantContext tenantContext)
    : IRequestHandler<CreateWhatsAppConversationLeadCommand, ApiResponse<WhatsAppConversationContextDto>>
{
    public async Task<ApiResponse<WhatsAppConversationContextDto>> Handle(
        CreateWhatsAppConversationLeadCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var conversation = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (conversation is null)
            return ApiResponse<WhatsAppConversationContextDto>.FailResponse("Conversation not found.");

        await crmLeadService.EnsureLeadLinkedAsync(
            tenantId,
            conversation,
            conversation.ContactPhone,
            conversation.ContactName,
            cancellationToken);

        var ctx = await inboxExtended.GetConversationContextAsync(tenantId, request.ConversationId, cancellationToken);
        return ctx is null
            ? ApiResponse<WhatsAppConversationContextDto>.FailResponse("Conversation not found after lead create.")
            : ApiResponse<WhatsAppConversationContextDto>.SuccessResponse(ctx);
    }
}
