using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.AI.Queries;

public record GetAiRecommendationsQuery(int TenantId)
    : IRequest<ApiResponse<IReadOnlyList<AiRecommendationDto>>>;

public sealed class GetAiRecommendationsQueryHandler(IAiRecommendationService recommendations)
    : IRequestHandler<GetAiRecommendationsQuery, ApiResponse<IReadOnlyList<AiRecommendationDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AiRecommendationDto>>> Handle(
        GetAiRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var list = await recommendations.GetActiveAsync(request.TenantId, cancellationToken);
        return ApiResponse<IReadOnlyList<AiRecommendationDto>>.SuccessResponse(list);
    }
}
