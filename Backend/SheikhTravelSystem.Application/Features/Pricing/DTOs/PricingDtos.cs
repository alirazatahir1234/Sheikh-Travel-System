namespace SheikhTravelSystem.Application.Features.Pricing.DTOs;

public record CalculatePriceRequest(
    int RouteId,
    int VehicleId,
    decimal FuelPrice,
    decimal DriverCost,
    decimal MaintenanceCost,
    decimal ProfitMargin);

public record PriceBreakdown(
    decimal Distance,
    decimal FuelAverage,
    decimal FuelPrice,
    decimal FuelCost,
    decimal DriverCost,
    decimal MaintenanceCost,
    decimal ProfitMargin,
    decimal TotalAmount);
