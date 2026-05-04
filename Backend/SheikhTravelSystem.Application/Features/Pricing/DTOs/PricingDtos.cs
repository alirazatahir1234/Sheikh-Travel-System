namespace SheikhTravelSystem.Application.Features.Pricing.DTOs;

public record CalculatePriceRequest(
    int RouteId,
    int VehicleId,
    decimal FuelPricePerLiter,
    decimal DriverAllowance,
    decimal TollCharges,
    decimal OtherCharges,
    bool IsRoundTrip = false);

public record PriceBreakdown(
    decimal Distance,
    decimal FuelAverage,
    decimal FuelPricePerLiter,
    decimal FuelCost,
    decimal DriverAllowance,
    decimal TollCharges,
    decimal OtherCharges,
    decimal TotalAmount,
    bool IsRoundTrip = false);
