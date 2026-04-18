using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Application.Features.Pricing.Commands;

public record CalculatePriceCommand(CalculatePriceRequest Request) : IRequest<ApiResponse<PriceBreakdown>>;

public class CalculatePriceCommandValidator : AbstractValidator<CalculatePriceCommand>
{
    public CalculatePriceCommandValidator()
    {
        RuleFor(x => x.Request.RouteId).GreaterThan(0);
        RuleFor(x => x.Request.VehicleId).GreaterThan(0);
        RuleFor(x => x.Request.FuelPrice).GreaterThan(0);
        RuleFor(x => x.Request.DriverCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.MaintenanceCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.ProfitMargin).GreaterThanOrEqualTo(0);
    }
}

public class CalculatePriceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CalculatePriceCommand, ApiResponse<PriceBreakdown>>
{
    public async Task<ApiResponse<PriceBreakdown>> Handle(CalculatePriceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var req = request.Request;

        var route = await connection.QuerySingleOrDefaultAsync<(decimal Distance, decimal BasePrice)?>
            (new CommandDefinition(
                "SELECT Distance, BasePrice FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                new { Id = req.RouteId },
                cancellationToken: cancellationToken));

        if (route is null)
            throw new NotFoundException("Route", req.RouteId);

        var fuelAverage = await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT FuelAverage FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = req.VehicleId },
                cancellationToken: cancellationToken));

        if (fuelAverage is null || fuelAverage <= 0)
            throw new NotFoundException("Vehicle", req.VehicleId);

        // Formula: FuelCost = (Distance / FuelAverage) × FuelPrice
        var fuelCost = (route.Value.Distance / fuelAverage.Value) * req.FuelPrice;
        var total = fuelCost + req.DriverCost + req.MaintenanceCost + req.ProfitMargin;

        var breakdown = new PriceBreakdown(
            Distance: route.Value.Distance,
            FuelAverage: fuelAverage.Value,
            FuelPrice: req.FuelPrice,
            FuelCost: Math.Round(fuelCost, 2),
            DriverCost: req.DriverCost,
            MaintenanceCost: req.MaintenanceCost,
            ProfitMargin: req.ProfitMargin,
            TotalAmount: Math.Round(total, 2));

        return ApiResponse<PriceBreakdown>.SuccessResponse(breakdown, "Price calculated successfully.");
    }
}
