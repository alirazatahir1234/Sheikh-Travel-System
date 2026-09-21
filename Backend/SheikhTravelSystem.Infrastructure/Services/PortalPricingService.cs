using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.Pricing.Commands;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Infrastructure.Services;

public sealed class PortalPricingService(ICustomerPortalRepository portalRepository) : IPortalPricingService
{
    public async Task<ApiResponse<PriceBreakdown>> CalculateQuoteAsync(
        ISender mediator,
        int routeId,
        int vehicleId,
        bool isRoundTrip,
        CancellationToken cancellationToken = default)
    {
        var basePrice = await portalRepository.GetRouteBasePriceAsync(routeId, cancellationToken);

        if (basePrice is null)
            return ApiResponse<PriceBreakdown>.FailResponse(
                "The selected route has changed or is no longer available. Please reload routes and select again.");

        return await mediator.Send(
            new CalculatePriceCommand(
                new CalculatePriceRequest(
                    routeId,
                    vehicleId,
                    PortalPricingDefaults.FuelPricePerLiter,
                    PortalPricingDefaults.DriverAllowance,
                    PortalPricingDefaults.TollCharges,
                    basePrice.Value,
                    isRoundTrip)),
            cancellationToken);
    }

    public async Task<ApiResponse<PriceBreakdown>> CalculatePointToPointQuoteAsync(
        ISender mediator,
        int vehicleId,
        double pickupLat,
        double pickupLng,
        double dropLat,
        double dropLng,
        bool isRoundTrip,
        int? routeId,
        CancellationToken cancellationToken = default)
    {
        var distanceKm = PortalDynamicPricingHelper.HaversineDistanceKm(pickupLat, pickupLng, dropLat, dropLng);
        if (distanceKm <= 0)
            return ApiResponse<PriceBreakdown>.FailResponse("Pickup and drop-off must be different locations.");

        if (distanceKm > PortalDynamicPricingHelper.MaxTripDistanceKm)
            return ApiResponse<PriceBreakdown>.FailResponse(
                $"Trips over {PortalDynamicPricingHelper.MaxTripDistanceKm} km require a custom quote. Contact support.");

        var vehicleOk = await portalRepository.VehicleExistsAndNotRetiredAsync(vehicleId, cancellationToken);
        if (!vehicleOk)
            return ApiResponse<PriceBreakdown>.FailResponse("Selected vehicle was not found.");

        if (routeId is > 0)
        {
            return await CalculateQuoteAsync(mediator, routeId.Value, vehicleId, isRoundTrip, cancellationToken);
        }

        var perKmBase = await portalRepository.GetPerKmBasePriceAsync(cancellationToken);
        var baseComponent = Math.Round(distanceKm * (perKmBase ?? 25m), 0);
        var effectiveRouteId = await portalRepository.GetFirstActiveRouteIdAsync(cancellationToken);

        if (effectiveRouteId <= 0)
            return ApiResponse<PriceBreakdown>.FailResponse("No active routes configured for pricing.");

        return await mediator.Send(
            new CalculatePriceCommand(
                new CalculatePriceRequest(
                    effectiveRouteId,
                    vehicleId,
                    PortalPricingDefaults.FuelPricePerLiter,
                    PortalPricingDefaults.DriverAllowance,
                    PortalPricingDefaults.TollCharges,
                    baseComponent,
                    isRoundTrip)),
            cancellationToken);
    }
}
