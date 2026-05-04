using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Application.Features.Pricing.Commands;

/// <summary>
/// Calculates price breakdown for a route/vehicle request.
/// </summary>
public record CalculatePriceCommand(CalculatePriceRequest Request) : IRequest<ApiResponse<PriceBreakdown>>;

/// <summary>
/// Validates price calculation inputs.
/// </summary>
public class CalculatePriceCommandValidator : AbstractValidator<CalculatePriceCommand>
{
    public CalculatePriceCommandValidator()
    {
        RuleFor(x => x.Request.RouteId).GreaterThan(0);
        RuleFor(x => x.Request.VehicleId).GreaterThan(0);
        RuleFor(x => x.Request.FuelPricePerLiter).GreaterThan(0);
        RuleFor(x => x.Request.DriverAllowance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.TollCharges).GreaterThanOrEqualTo(0);
        // Allow negative values for discount/adjustment (e.g. -500).
        RuleFor(x => x.Request.OtherCharges).GreaterThanOrEqualTo(-1_000_000);
    }
}

/// <summary>
/// Computes booking price components from route and vehicle data.
/// </summary>
public class CalculatePriceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CalculatePriceCommand, ApiResponse<PriceBreakdown>>
{
    /// <summary>
    /// Applies pricing formula and returns a detailed breakdown.
    /// </summary>
    public async Task<ApiResponse<PriceBreakdown>> Handle(CalculatePriceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var req = request.Request;

        var route = await connection.QuerySingleOrDefaultAsync<(decimal Distance, decimal BasePrice)?>
            (new CommandDefinition(
                "SELECT Distance, BasePrice FROM Routes WHERE Id = @Id",
                new { Id = req.RouteId },
                cancellationToken: cancellationToken));

        if (route is null)
            return ApiResponse<PriceBreakdown>.FailResponse("Selected route was not found. Please reselect the route.");

        var fuelAverage = await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT FuelAverage FROM Vehicles WHERE Id = @Id",
                new { Id = req.VehicleId },
                cancellationToken: cancellationToken));

        if (fuelAverage is null || fuelAverage <= 0)
            return ApiResponse<PriceBreakdown>.FailResponse("Selected vehicle was not found or has invalid fuel average.");

        // Formula: FuelCost = (Distance / FuelAverage) × FuelPricePerLiter × TripMultiplier
        // TripMultiplier = 2 for round trips (driver must return), 1 for one-way
        var tripMultiplier = req.IsRoundTrip ? 2.0m : 1.0m;
        var fuelCost = (route.Value.Distance / fuelAverage.Value) * req.FuelPricePerLiter * tripMultiplier;
        var total = fuelCost + req.DriverAllowance + req.TollCharges + req.OtherCharges;

        var breakdown = new PriceBreakdown(
            Distance: route.Value.Distance,
            FuelAverage: fuelAverage.Value,
            FuelPricePerLiter: req.FuelPricePerLiter,
            FuelCost: Math.Round(fuelCost, 2),
            DriverAllowance: req.DriverAllowance,
            TollCharges: req.TollCharges,
            OtherCharges: req.OtherCharges,
            TotalAmount: Math.Round(total, 2),
            IsRoundTrip: req.IsRoundTrip);

        return ApiResponse<PriceBreakdown>.SuccessResponse(breakdown, "Price calculated successfully.");
    }
}
