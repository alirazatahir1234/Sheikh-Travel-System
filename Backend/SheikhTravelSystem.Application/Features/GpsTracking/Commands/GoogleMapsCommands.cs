using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record OptimizeDispatchToursCommand(OptimizeDispatchToursRequestDto Request)
    : IRequest<ApiResponse<OptimizeDispatchToursResultDto>>;

public class OptimizeDispatchToursCommandHandler(IGoogleRouteOptimizationService optimizationService)
    : IRequestHandler<OptimizeDispatchToursCommand, ApiResponse<OptimizeDispatchToursResultDto>>
{
    public async Task<ApiResponse<OptimizeDispatchToursResultDto>> Handle(
        OptimizeDispatchToursCommand request, CancellationToken cancellationToken)
    {
        if (request.Request.Vehicles.Count == 0 || request.Request.Shipments.Count == 0)
        {
            return ApiResponse<OptimizeDispatchToursResultDto>.FailResponse(
                "At least one vehicle and one shipment are required.");
        }

        var result = await optimizationService.OptimizeToursAsync(request.Request, cancellationToken);
        if (result is null)
        {
            return ApiResponse<OptimizeDispatchToursResultDto>.FailResponse(
                "Tour optimization is unavailable. Ensure Google Maps API key and Cloud project are configured.");
        }

        return ApiResponse<OptimizeDispatchToursResultDto>.SuccessResponse(result);
    }
}
