using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.AI.Commands;

public record QueryAiAssistantCommand(
    int TenantId,
    int UserId,
    string Question,
    bool IsPlatformSuperAdmin = false) : IRequest<ApiResponse<AiAssistantQueryResponse>>;

public sealed class QueryAiAssistantCommandHandler(IAiAssistantGateway assistant)
    : IRequestHandler<QueryAiAssistantCommand, ApiResponse<AiAssistantQueryResponse>>
{
    public async Task<ApiResponse<AiAssistantQueryResponse>> Handle(
        QueryAiAssistantCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return ApiResponse<AiAssistantQueryResponse>.FailResponse("Question is required.");

        if (request.TenantId <= 0)
            return ApiResponse<AiAssistantQueryResponse>.FailResponse("Tenant context is required.");

        if (request.UserId <= 0)
            return ApiResponse<AiAssistantQueryResponse>.FailResponse("User context is required.");

        var result = await assistant.QueryAsync(
            request.TenantId,
            request.UserId,
            new AiAssistantQueryRequest(request.Question.Trim(), request.IsPlatformSuperAdmin),
            cancellationToken);

        return ApiResponse<AiAssistantQueryResponse>.SuccessResponse(result);
    }
}
