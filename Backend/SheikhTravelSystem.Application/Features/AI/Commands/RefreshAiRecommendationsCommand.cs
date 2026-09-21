using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.AI.Commands;

/// <summary>AI Operations: refresh recommendation pipeline (mutation only).</summary>
public record RefreshAiRecommendationsCommand(int TenantId)
    : IRequest<ApiResponse<bool>>;

public sealed class RefreshAiRecommendationsCommandHandler(IAiRecommendationService recommendations)
    : IRequestHandler<RefreshAiRecommendationsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        RefreshAiRecommendationsCommand request,
        CancellationToken cancellationToken)
    {
        await recommendations.RefreshAsync(request.TenantId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true);
    }
}
