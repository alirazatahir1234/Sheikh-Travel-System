using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record UpsertDriverScoreWeightsCommand(UpsertDriverScoreWeightsRequest Body)
    : IRequest<ApiResponse<DriverScoreWeightsDto>>, IAuditableCommand
{
    public string AuditAction => "UpdateDriverPerformanceWeights";
    public string AuditEntityName => "DriverPerformanceScoreWeights";
    public int? AuditEntityId => null;
}

public class UpsertDriverScoreWeightsCommandValidator : AbstractValidator<UpsertDriverScoreWeightsCommand>
{
    public UpsertDriverScoreWeightsCommandValidator()
    {
        RuleFor(x => x.Body.SafetyWeight).InclusiveBetween(0, 1000);
        RuleFor(x => x.Body.FuelWeight).InclusiveBetween(0, 1000);
        RuleFor(x => x.Body.OnTimeWeight).InclusiveBetween(0, 1000);
        RuleFor(x => x)
            .Must(x => x.Body.SafetyWeight + x.Body.FuelWeight + x.Body.OnTimeWeight > 0)
            .WithMessage("At least one weight must be greater than zero.");
    }
}

public class UpsertDriverScoreWeightsCommandHandler(
    IDriverPerformanceScoringRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IAppCache cache)
    : IRequestHandler<UpsertDriverScoreWeightsCommand, ApiResponse<DriverScoreWeightsDto>>
{
    public async Task<ApiResponse<DriverScoreWeightsDto>> Handle(
        UpsertDriverScoreWeightsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var weights = await repository.UpsertWeightsAsync(
            tenantId,
            request.Body,
            currentUser.UserId?.ToString() ?? "api",
            cancellationToken);

        // Ranking cache includes weight fingerprint, but bust tenant prefix so reload is immediate.
        cache.RemoveByPrefix($"{GetDriverScoreRankingQueryHandler.CacheKeyPrefix}{tenantId}:");

        return ApiResponse<DriverScoreWeightsDto>.SuccessResponse(
            DriverScoreCalculator.NormalizeWeights(weights.SafetyWeight, weights.FuelWeight, weights.OnTimeWeight),
            "Driver performance weights updated.");
    }
}
