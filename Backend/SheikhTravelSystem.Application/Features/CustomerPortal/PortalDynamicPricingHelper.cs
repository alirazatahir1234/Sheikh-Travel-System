using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Pricing.Commands;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal;

public static class PortalDynamicPricingHelper
{
    public const decimal MaxTripDistanceKm = 800;

    public static decimal HaversineDistanceKm(double lat1, double lng1, double lat2, double lng2)
        => (decimal)GpsGeoHelper.HaversineKm(lat1, lng1, lat2, lng2);

    public static int EstimateDurationMinutes(decimal distanceKm)
        => distanceKm <= 0 ? 60 : (int)Math.Ceiling((double)distanceKm / 70.0 * 60);

    public static async Task<ApiResponse<PriceBreakdown>> CalculatePointToPointQuoteAsync(
        ISender mediator,
        IDbConnectionFactory dbFactory,
        int vehicleId,
        double pickupLat,
        double pickupLng,
        double dropLat,
        double dropLng,
        bool isRoundTrip,
        int? routeId,
        CancellationToken cancellationToken)
    {
        var distanceKm = HaversineDistanceKm(pickupLat, pickupLng, dropLat, dropLng);
        if (distanceKm <= 0)
            return ApiResponse<PriceBreakdown>.FailResponse("Pickup and drop-off must be different locations.");

        if (distanceKm > MaxTripDistanceKm)
            return ApiResponse<PriceBreakdown>.FailResponse($"Trips over {MaxTripDistanceKm} km require a custom quote. Contact support.");

        using var connection = dbFactory.CreateConnection();
        var vehicleOk = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND IsDeleted = 0 AND Status <> 4) THEN 1 ELSE 0 END",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));

        if (!vehicleOk)
            return ApiResponse<PriceBreakdown>.FailResponse("Selected vehicle was not found.");

        decimal baseComponent;
        int effectiveRouteId;

        if (routeId is > 0)
        {
            var catalog = await PortalPricingHelper.CalculateQuoteAsync(
                mediator, dbFactory, routeId.Value, vehicleId, isRoundTrip, cancellationToken);
            if (!catalog.Success || catalog.Data is null)
                return catalog;
            return catalog;
        }

        var perKmBase = await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT TOP 1 BasePrice / NULLIF(Distance, 0) FROM Routes WHERE IsDeleted = 0 AND IsActive = 1 AND Distance > 0 ORDER BY Id",
                cancellationToken: cancellationToken));

        baseComponent = Math.Round(distanceKm * (perKmBase ?? 25m), 0);
        effectiveRouteId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT TOP 1 Id FROM Routes WHERE IsDeleted = 0 AND IsActive = 1 ORDER BY Id",
                cancellationToken: cancellationToken));

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
