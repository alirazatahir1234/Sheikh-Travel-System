using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
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
    ICurrentUserService currentUser)
    : IRequestHandler<UpsertDriverScoreWeightsCommand, ApiResponse<DriverScoreWeightsDto>>
{
    public async Task<ApiResponse<DriverScoreWeightsDto>> Handle(
        UpsertDriverScoreWeightsCommand request, CancellationToken cancellationToken)
    {
        var weights = await repository.UpsertWeightsAsync(
            tenantContext.GetRequiredTenantId(),
            request.Body,
            currentUser.UserId?.ToString() ?? "api",
            cancellationToken);
        return ApiResponse<DriverScoreWeightsDto>.SuccessResponse(
            DriverScoreCalculator.NormalizeWeights(weights.SafetyWeight, weights.FuelWeight, weights.OnTimeWeight),
            "Driver performance weights updated.");
    }
}
