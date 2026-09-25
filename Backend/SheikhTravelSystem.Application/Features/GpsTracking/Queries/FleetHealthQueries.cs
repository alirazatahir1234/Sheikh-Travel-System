using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

/// <summary>
/// Explainable Live Map fleet health — real maintenance / alerts / GPS device factors only.
/// </summary>
public record GetFleetHealthQuery : IRequest<ApiResponse<FleetHealthSummaryDto>>;

public class GetFleetHealthQueryHandler(IGpsFleetHealthCalculator calculator)
    : IRequestHandler<GetFleetHealthQuery, ApiResponse<FleetHealthSummaryDto>>
{
    public async Task<ApiResponse<FleetHealthSummaryDto>> Handle(
        GetFleetHealthQuery request,
        CancellationToken cancellationToken)
    {
        var summary = await calculator.ComputeAsync(cancellationToken);
        return ApiResponse<FleetHealthSummaryDto>.SuccessResponse(summary);
    }
}
