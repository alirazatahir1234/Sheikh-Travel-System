using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Portal fare quotes. SQL for route/vehicle lookups lives in Infrastructure.
/// </summary>
public interface IPortalPricingService
{
    Task<ApiResponse<PriceBreakdown>> CalculateQuoteAsync(
        ISender mediator,
        int routeId,
        int vehicleId,
        bool isRoundTrip,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PriceBreakdown>> CalculatePointToPointQuoteAsync(
        ISender mediator,
        int vehicleId,
        double pickupLat,
        double pickupLng,
        double dropLat,
        double dropLng,
        bool isRoundTrip,
        int? routeId,
        CancellationToken cancellationToken = default);
}
