using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.AI.Queries;

public record GetAiProviderHealthQuery(int TenantId)
    : IRequest<ApiResponse<AiProviderHealthDto>>;

public sealed class GetAiProviderHealthQueryHandler(IAiChatGateway chatGateway)
    : IRequestHandler<GetAiProviderHealthQuery, ApiResponse<AiProviderHealthDto>>
{
    public async Task<ApiResponse<AiProviderHealthDto>> Handle(
        GetAiProviderHealthQuery request,
        CancellationToken cancellationToken)
    {
        var health = await chatGateway.GetProviderHealthAsync(request.TenantId, cancellationToken);
        return ApiResponse<AiProviderHealthDto>.SuccessResponse(health);
    }
}
