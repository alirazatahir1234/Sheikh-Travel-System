using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Pricing.Commands;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal;

/// <summary>
/// Computes a portal price quote using the same engine as staff pricing, with portal defaults
/// and route base price included in <see cref="CalculatePriceRequest.OtherCharges"/>.
/// </summary>
public static class PortalPricingHelper
{
    public static async Task<ApiResponse<PriceBreakdown>> CalculateQuoteAsync(
        ISender mediator,
        IDbConnectionFactory dbFactory,
        int routeId,
        int vehicleId,
        bool isRoundTrip,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var basePrice = await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT BasePrice FROM Routes WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1",
                new { Id = routeId },
                cancellationToken: cancellationToken));

        if (basePrice is null)
            return ApiResponse<PriceBreakdown>.FailResponse("Selected route was not found.");

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
}
