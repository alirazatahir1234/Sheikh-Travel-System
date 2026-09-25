using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.AiAssist;

public record SuggestWhatsAppAiReplyCommand(
    int ConversationId,
    string? PriorSuggestion = null,
    string? Instruction = null)
    : IRequest<ApiResponse<WhatsAppAiAssistResultDto>>;

public class SuggestWhatsAppAiReplyCommandHandler(
    WhatsAppAiAssistRunner assist,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<SuggestWhatsAppAiReplyCommand, ApiResponse<WhatsAppAiAssistResultDto>>
{
    public Task<ApiResponse<WhatsAppAiAssistResultDto>> Handle(
        SuggestWhatsAppAiReplyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(WhatsAppPermissions.AiAssist))
            return Task.FromResult(ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                "WhatsApp.AiAssist permission is required.", code: "FORBIDDEN"));

        var tenantId = tenantContext.GetRequiredTenantId();
        var userId = currentUser.UserId ?? 0;
        return assist.SuggestAsync(
            tenantId,
            userId,
            request.ConversationId,
            new WhatsAppAiAssistSuggestRequest(request.PriorSuggestion, request.Instruction),
            cancellationToken);
    }
}

public record TransformWhatsAppAiReplyCommand(
    int ConversationId,
    string Action,
    string? Text = null,
    string? TargetLanguage = null,
    string? PriorSuggestion = null)
    : IRequest<ApiResponse<WhatsAppAiAssistResultDto>>;

public class TransformWhatsAppAiReplyCommandHandler(
    WhatsAppAiAssistRunner assist,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<TransformWhatsAppAiReplyCommand, ApiResponse<WhatsAppAiAssistResultDto>>
{
    public Task<ApiResponse<WhatsAppAiAssistResultDto>> Handle(
        TransformWhatsAppAiReplyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(WhatsAppPermissions.AiAssist))
            return Task.FromResult(ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                "WhatsApp.AiAssist permission is required.", code: "FORBIDDEN"));

        var tenantId = tenantContext.GetRequiredTenantId();
        var userId = currentUser.UserId ?? 0;
        return assist.TransformAsync(
            tenantId,
            userId,
            request.ConversationId,
            new WhatsAppAiAssistTransformRequest(
                request.Action,
                request.Text,
                request.TargetLanguage,
                request.PriorSuggestion),
            cancellationToken);
    }
}

public record SummarizeWhatsAppConversationCommand(int ConversationId)
    : IRequest<ApiResponse<WhatsAppAiAssistResultDto>>;

public class SummarizeWhatsAppConversationCommandHandler(
    WhatsAppAiAssistRunner assist,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<SummarizeWhatsAppConversationCommand, ApiResponse<WhatsAppAiAssistResultDto>>
{
    public Task<ApiResponse<WhatsAppAiAssistResultDto>> Handle(
        SummarizeWhatsAppConversationCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(WhatsAppPermissions.AiAssist))
            return Task.FromResult(ApiResponse<WhatsAppAiAssistResultDto>.FailResponse(
                "WhatsApp.AiAssist permission is required.", code: "FORBIDDEN"));

        var tenantId = tenantContext.GetRequiredTenantId();
        var userId = currentUser.UserId ?? 0;
        return assist.SummarizeAsync(tenantId, userId, request.ConversationId, cancellationToken);
    }
}
