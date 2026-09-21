using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.AI.Commands;

/// <summary>AI Operations: capture features + run heuristic / model predictions.</summary>
public record RunAiPredictionsCommand(int TenantId)
    : IRequest<ApiResponse<IReadOnlyList<AiPredictionDto>>>;

public sealed class RunAiPredictionsCommandHandler(IAiPredictionService predictions)
    : IRequestHandler<RunAiPredictionsCommand, ApiResponse<IReadOnlyList<AiPredictionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AiPredictionDto>>> Handle(
        RunAiPredictionsCommand request,
        CancellationToken cancellationToken)
    {
        await predictions.CaptureFeaturesAsync(request.TenantId, cancellationToken);
        await predictions.RunHeuristicPredictionsAsync(request.TenantId, cancellationToken);
        var list = await predictions.GetPredictionsAsync(request.TenantId, null, cancellationToken);
        return ApiResponse<IReadOnlyList<AiPredictionDto>>.SuccessResponse(list);
    }
}
