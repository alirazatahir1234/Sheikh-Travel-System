using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.AI.Commands;

/// <summary>AI Operations: chat turn via <see cref="IAiChatGateway"/> (provider-agnostic).</summary>
public record SendAiChatCommand(
    int TenantId,
    int UserId,
    string Message,
    Guid? SessionId = null,
    string? Title = null,
    bool ConfirmWrite = false) : IRequest<ApiResponse<AiChatTurnResponse>>;

public sealed class SendAiChatCommandHandler(IAiChatGateway chatGateway)
    : IRequestHandler<SendAiChatCommand, ApiResponse<AiChatTurnResponse>>
{
    public async Task<ApiResponse<AiChatTurnResponse>> Handle(
        SendAiChatCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) && !request.ConfirmWrite)
            return ApiResponse<AiChatTurnResponse>.FailResponse("Message is required.");

        if (request.ConfirmWrite && request.SessionId is null)
            return ApiResponse<AiChatTurnResponse>.FailResponse("SessionId is required to confirm a pending action.");

        var result = await chatGateway.ChatAsync(
            request.TenantId,
            request.UserId,
            new AiChatTurnRequest(
                request.Message ?? string.Empty,
                request.SessionId,
                request.Title,
                request.ConfirmWrite),
            cancellationToken);

        return ApiResponse<AiChatTurnResponse>.SuccessResponse(result);
    }
}
