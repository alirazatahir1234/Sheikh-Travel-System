using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.AI.Queries;

public record GetAiPredictionsQuery(int TenantId, string? EntityType = null)
    : IRequest<ApiResponse<IReadOnlyList<AiPredictionDto>>>;

public sealed class GetAiPredictionsQueryHandler(IAiPredictionService predictions)
    : IRequestHandler<GetAiPredictionsQuery, ApiResponse<IReadOnlyList<AiPredictionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AiPredictionDto>>> Handle(
        GetAiPredictionsQuery request,
        CancellationToken cancellationToken)
    {
        var list = await predictions.GetPredictionsAsync(
            request.TenantId, request.EntityType, cancellationToken);
        return ApiResponse<IReadOnlyList<AiPredictionDto>>.SuccessResponse(list);
    }
}
