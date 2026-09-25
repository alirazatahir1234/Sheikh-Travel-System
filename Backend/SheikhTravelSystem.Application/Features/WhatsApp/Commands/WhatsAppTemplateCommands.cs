using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record UpsertWhatsAppTemplateCommand(
    int WhatsAppAccountId,
    string Name,
    string Language,
    string Category,
    string Status,
    string? MetaTemplateId = null,
    string? BodyPreview = null)
    : IRequest<ApiResponse<WhatsAppTemplateDto>>, IAuditableCommand
{
    public string AuditAction => "Upsert";
    public string AuditEntityName => "WhatsAppTemplate";
    public int? AuditEntityId => null;
}

public class UpsertWhatsAppTemplateCommandValidator : AbstractValidator<UpsertWhatsAppTemplateCommand>
{
    public UpsertWhatsAppTemplateCommandValidator()
    {
        RuleFor(x => x.WhatsAppAccountId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Language).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Status).Must(s => WhatsAppTemplateStatuses.All.Contains(s))
            .WithMessage("Invalid template status.");
    }
}

public class UpsertWhatsAppTemplateCommandHandler(
    IWhatsAppTemplateRepository repository,
    IWhatsAppInboxRepository inbox,
    ITenantContext tenantContext)
    : IRequestHandler<UpsertWhatsAppTemplateCommand, ApiResponse<WhatsAppTemplateDto>>
{
    public async Task<ApiResponse<WhatsAppTemplateDto>> Handle(
        UpsertWhatsAppTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var account = await inbox.GetAccountByIdAsync(tenantId, request.WhatsAppAccountId, cancellationToken);
        if (account is null)
            return ApiResponse<WhatsAppTemplateDto>.FailResponse("WhatsApp account not found.");

        var row = await repository.UpsertAsync(
            tenantId,
            request.WhatsAppAccountId,
            request.Name,
            request.Language,
            request.Category,
            request.Status,
            request.MetaTemplateId,
            request.BodyPreview,
            cancellationToken);
        return ApiResponse<WhatsAppTemplateDto>.SuccessResponse(row!, "Template saved.");
    }
}

public record SetWhatsAppTemplateStatusCommand(
    int Id,
    string Status,
    string? MetaTemplateId = null)
    : IRequest<ApiResponse<WhatsAppTemplateDto>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "WhatsAppTemplate";
    public int? AuditEntityId => Id;
}

public class SetWhatsAppTemplateStatusCommandValidator : AbstractValidator<SetWhatsAppTemplateStatusCommand>
{
    public SetWhatsAppTemplateStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => WhatsAppTemplateStatuses.All.Contains(s))
            .WithMessage("Invalid template status.");
    }
}

public class SetWhatsAppTemplateStatusCommandHandler(
    IWhatsAppTemplateRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<SetWhatsAppTemplateStatusCommand, ApiResponse<WhatsAppTemplateDto>>
{
    public async Task<ApiResponse<WhatsAppTemplateDto>> Handle(
        SetWhatsAppTemplateStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await repository.SetStatusAsync(
            tenantId, request.Id, request.Status, request.MetaTemplateId, cancellationToken);
        return row is null
            ? ApiResponse<WhatsAppTemplateDto>.FailResponse("Template not found.")
            : ApiResponse<WhatsAppTemplateDto>.SuccessResponse(row, "Status updated.");
    }
}

public record SendWhatsAppTemplateCommand(
    int? ConversationId,
    int? WhatsAppAccountId,
    string? RecipientPhoneNumber,
    string TemplateName,
    string Language = "en",
    IReadOnlyList<string>? BodyParameters = null)
    : IRequest<ApiResponse<SendWhatsAppMessageResultDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "WhatsAppMessage";
    public int? AuditEntityId => null;
}

public class SendWhatsAppTemplateCommandValidator : AbstractValidator<SendWhatsAppTemplateCommand>
{
    public SendWhatsAppTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Language).NotEmpty().MaximumLength(20);
        RuleFor(x => x)
            .Must(x => x.ConversationId is > 0 || !string.IsNullOrWhiteSpace(x.RecipientPhoneNumber))
            .WithMessage("ConversationId or RecipientPhoneNumber is required.");
    }
}

public class SendWhatsAppTemplateCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppTemplateRepository templates,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppAccountConfig accountConfig,
    IWhatsAppAccountResolver accountResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IWhatsAppRealtimePublisher realtime)
    : IRequestHandler<SendWhatsAppTemplateCommand, ApiResponse<SendWhatsAppMessageResultDto>>
{
    public async Task<ApiResponse<SendWhatsAppMessageResultDto>> Handle(
        SendWhatsAppTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        string? recipient = request.RecipientPhoneNumber;
        WhatsAppConversationDto? conversation = null;
        int? conversationAccountId = null;

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

        WhatsAppAccountRow? account;
        if (request.WhatsAppAccountId is > 0)
            account = await accountResolver.ResolveAsync(tenantId, request.WhatsAppAccountId, recipient, cancellationToken);
        else if (conversationAccountId is > 0)
            account = await repository.GetAccountByIdAsync(tenantId, conversationAccountId.Value, cancellationToken);
        else
            account = await accountResolver.ResolveAsync(tenantId, null, recipient, cancellationToken);

        if (account is null || !account.IsActive)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("WhatsApp account not found or inactive.");

        if (accountConfig.ResolveCredentials(account) is null)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(
                "WhatsApp credentials are not configured for this account.");

        var language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim();
        var template = await templates.GetByNameAsync(
            tenantId, account.Id, request.TemplateName, language, cancellationToken);
        if (template is null)
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse("Template not found in catalog.");
        if (!string.Equals(template.Status, WhatsAppTemplateStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(
                $"Template '{template.Name}' is not Approved (status={template.Status}).");

        var conversationId = conversation?.Id
            ?? await repository.EnsureConversationAsync(
                tenantId, account.Id, WhatsAppPhone.ToE164(recipient), null, cancellationToken);

        if (currentUser.UserId is > 0)
        {
            await repository.SetConversationBotAsync(
                tenantId, conversationId, false, WhatsAppDemoBotStates.HandedOff, cancellationToken);
            if (conversation?.AssignedUserId is null)
                await repository.SetConversationAssignmentAsync(tenantId, conversationId, currentUser.UserId, cancellationToken);
        }

        var preview = $"Template: {template.Name} ({language})";
        var pendingId = await repository.InsertOutboundMessageAsync(
            tenantId, conversationId, null, "template", preview, "Queued", cancellationToken, template.Name);
        await repository.SetOutboundStatusAsync(pendingId, "Sending", null, cancellationToken);

        var send = await cloudApi.SendTemplateAsync(
            account,
            WhatsAppPhone.ToApiDigits(recipient),
            template.Name,
            language,
            request.BodyParameters,
            cancellationToken);

        if (!send.Success)
        {
            await repository.SetOutboundMetaIdAsync(
                pendingId, send.MetaMessageId ?? $"fail-{pendingId}", "Failed", cancellationToken);
            await repository.SetOutboundStatusAsync(pendingId, "Failed", send.ErrorMessage, cancellationToken);
            return ApiResponse<SendWhatsAppMessageResultDto>.FailResponse(send.ErrorMessage ?? "Template send failed.");
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
            "Template sent.");
    }
}
