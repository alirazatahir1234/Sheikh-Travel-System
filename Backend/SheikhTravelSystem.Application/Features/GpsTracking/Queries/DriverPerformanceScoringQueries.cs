using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetDriverScoreWeightsQuery() : IRequest<ApiResponse<DriverScoreWeightsDto>>;

public class GetDriverScoreWeightsQueryHandler(
    IDriverPerformanceScoringRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverScoreWeightsQuery, ApiResponse<DriverScoreWeightsDto>>
{
    public async Task<ApiResponse<DriverScoreWeightsDto>> Handle(
        GetDriverScoreWeightsQuery request, CancellationToken cancellationToken)
    {
        var weights = await repository.GetOrCreateWeightsAsync(
            tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<DriverScoreWeightsDto>.SuccessResponse(weights);
    }
}

public record GetDriverPerformanceDetailQuery(
    int DriverId,
    DateTime? FromDate = null,
    DateTime? ToDate = null)
    : IRequest<ApiResponse<DriverPerformanceDetailDto>>;

public class GetDriverPerformanceDetailQueryValidator : AbstractValidator<GetDriverPerformanceDetailQuery>
{
    public GetDriverPerformanceDetailQueryValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
    }
}

public class GetDriverPerformanceDetailQueryHandler(
    IMediator mediator,
    IDriverPerformanceScoringRepository scoringRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverPerformanceDetailQuery, ApiResponse<DriverPerformanceDetailDto>>
{
    public async Task<ApiResponse<DriverPerformanceDetailDto>> Handle(
        GetDriverPerformanceDetailQuery request, CancellationToken cancellationToken)
    {
        var from = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = request.ToDate ?? DateTime.UtcNow;
        // Reuse cached SQL ranking (same as KPI strip). History / supporting events stay on-demand.
        var ranking = await mediator.Send(
            new GetDriverScoreRankingQuery(from, to, IncludeTraccarEnrichment: false),
            cancellationToken);
        if (!ranking.Success || ranking.Data is null)
            return ApiResponse<DriverPerformanceDetailDto>.FailResponse(ranking.Message ?? "Failed to load scores.");

        var score = ranking.Data.FirstOrDefault(r => r.DriverId == request.DriverId);
        if (score is null)
        {
            score = new DriverScoreDto(
                request.DriverId,
                $"Driver #{request.DriverId}",
                0,
                "N/A",
                new DriverScoreFactorsDto(0, 0, 0, 0, 0, 0, null),
                IsPartial: true,
                ScoreExplanation: "No attributed GPS trips in this period.");
        }

        var tenantId = tenantContext.GetRequiredTenantId();
        var history = await scoringRepository.GetHistoryAsync(
            tenantId, request.DriverId, from.AddDays(-90), to, 40, cancellationToken);
        var events = await scoringRepository.GetSupportingEventsAsync(
            tenantId, request.DriverId, from, to, 50, cancellationToken);

        return ApiResponse<DriverPerformanceDetailDto>.SuccessResponse(
            new DriverPerformanceDetailDto(score, history, events));
    }
}

public record GetDriverScoreHistoryQuery(
    int DriverId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Limit = 40)
    : IRequest<ApiResponse<List<DriverScoreHistoryDto>>>;

public class GetDriverScoreHistoryQueryHandler(
    IDriverPerformanceScoringRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverScoreHistoryQuery, ApiResponse<List<DriverScoreHistoryDto>>>
{
    public async Task<ApiResponse<List<DriverScoreHistoryDto>>> Handle(
        GetDriverScoreHistoryQuery request, CancellationToken cancellationToken)
    {
        var rows = await repository.GetHistoryAsync(
            tenantContext.GetRequiredTenantId(),
            request.DriverId,
            request.FromDate,
            request.ToDate,
            request.Limit,
            cancellationToken);
        return ApiResponse<List<DriverScoreHistoryDto>>.SuccessResponse(rows.ToList());
    }
}
