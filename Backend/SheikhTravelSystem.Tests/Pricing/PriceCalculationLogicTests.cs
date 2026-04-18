using FluentAssertions;
using SheikhTravelSystem.Application.Features.Pricing.Commands;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Tests.Pricing;

public class PriceCalculationLogicTests
{
    // Mirrors CalculatePriceCommandHandler formula:
    // FuelCost = (Distance / FuelAverage) × FuelPrice
    // Total    = FuelCost + DriverCost + MaintenanceCost + ProfitMargin
    private static decimal CalculateFuelCost(decimal distance, decimal fuelAverage, decimal fuelPrice)
        => distance / fuelAverage * fuelPrice;

    private static decimal CalculateTotal(decimal fuelCost, decimal driverCost, decimal maintenanceCost, decimal profitMargin)
        => fuelCost + driverCost + maintenanceCost + profitMargin;

    [Fact]
    public void FuelCost_BasicCalc_ShouldBeCorrect()
    {
        // 300km / 10km per litre * 250 per litre = 7500
        var fuelCost = CalculateFuelCost(300m, 10m, 250m);
        fuelCost.Should().Be(7500m);
    }

    [Fact]
    public void FuelCost_FractionalFuelAverage_ShouldBeCorrect()
    {
        // 200km / 12.5km per litre * 300 per litre = 4800
        var fuelCost = CalculateFuelCost(200m, 12.5m, 300m);
        fuelCost.Should().Be(4800m);
    }

    [Fact]
    public void TotalAmount_IncludesAllCostComponents()
    {
        var fuelCost = CalculateFuelCost(300m, 10m, 250m); // 7500
        var total = CalculateTotal(fuelCost, 2000m, 500m, 1000m);
        total.Should().Be(11000m);
    }

    [Fact]
    public void TotalAmount_WithZeroOptionalCosts_EqualsOnlyFuelCost()
    {
        var fuelCost = CalculateFuelCost(100m, 10m, 200m); // 2000
        var total = CalculateTotal(fuelCost, 0m, 0m, 0m);
        total.Should().Be(2000m);
    }

    [Fact]
    public void FuelCost_LongDistance_ShouldScaleCorrectly()
    {
        // 600km / 8km per litre * 280 per litre = 21000
        var fuelCost = CalculateFuelCost(600m, 8m, 280m);
        fuelCost.Should().Be(21000m);
    }

    [Fact]
    public void FuelCost_HighFuelEfficiency_ShouldBeLower()
    {
        var lowEff = CalculateFuelCost(200m, 8m, 300m);   // 7500
        var highEff = CalculateFuelCost(200m, 15m, 300m);  // 4000
        highEff.Should().BeLessThan(lowEff);
    }

    [Fact]
    public void FuelCost_HigherFuelPrice_ShouldIncreaseCost()
    {
        var lowPrice = CalculateFuelCost(200m, 10m, 200m);  // 4000
        var highPrice = CalculateFuelCost(200m, 10m, 300m); // 6000
        highPrice.Should().BeGreaterThan(lowPrice);
    }

    [Fact]
    public void RoundedFuelCost_ShouldMatchExpected()
    {
        var raw = CalculateFuelCost(150m, 12m, 260m); // 3250
        Math.Round(raw, 2).Should().Be(3250.00m);
    }

    [Fact]
    public void PriceBreakdown_ShouldHoldAllFields()
    {
        var breakdown = new PriceBreakdown(
            Distance: 300m,
            FuelAverage: 10m,
            FuelPrice: 250m,
            FuelCost: 7500m,
            DriverCost: 2000m,
            MaintenanceCost: 500m,
            ProfitMargin: 1000m,
            TotalAmount: 11000m);

        breakdown.Distance.Should().Be(300m);
        breakdown.FuelAverage.Should().Be(10m);
        breakdown.FuelPrice.Should().Be(250m);
        breakdown.FuelCost.Should().Be(7500m);
        breakdown.DriverCost.Should().Be(2000m);
        breakdown.MaintenanceCost.Should().Be(500m);
        breakdown.ProfitMargin.Should().Be(1000m);
        breakdown.TotalAmount.Should().Be(11000m);
    }

    [Fact]
    public void CalculatePriceRequest_ShouldHoldAllFields()
    {
        var req = new CalculatePriceRequest(
            RouteId: 1, VehicleId: 2, FuelPrice: 250m,
            DriverCost: 2000m, MaintenanceCost: 500m, ProfitMargin: 1000m);

        req.RouteId.Should().Be(1);
        req.VehicleId.Should().Be(2);
        req.FuelPrice.Should().Be(250m);
        req.DriverCost.Should().Be(2000m);
        req.MaintenanceCost.Should().Be(500m);
        req.ProfitMargin.Should().Be(1000m);
    }

    [Fact]
    public void CalculatePriceCommand_ShouldWrapRequest()
    {
        var req = new CalculatePriceRequest(1, 2, 250m, 0m, 0m, 0m);
        var cmd = new CalculatePriceCommand(req);
        cmd.Request.Should().Be(req);
    }

    [Fact]
    public void ProfitMargin_IsAddedDirectlyToTotal()
    {
        var fuelCost = CalculateFuelCost(100m, 10m, 200m); // 2000
        var withProfit = CalculateTotal(fuelCost, 0m, 0m, 500m);
        var withoutProfit = CalculateTotal(fuelCost, 0m, 0m, 0m);
        (withProfit - withoutProfit).Should().Be(500m);
    }

    [Theory]
    [InlineData(100, 10, 200, 0, 0, 0, 2000)]
    [InlineData(200, 10, 250, 1000, 500, 500, 7000)]
    [InlineData(500, 12.5, 300, 3000, 1000, 2000, 18000)]
    public void TotalAmount_MultipleScenarios_ShouldBeCorrect(
        double distance, double fuelAvg, double fuelPrice,
        double driverCost, double maintCost, double profit,
        double expectedTotal)
    {
        var fuelCost = CalculateFuelCost((decimal)distance, (decimal)fuelAvg, (decimal)fuelPrice);
        var total = CalculateTotal(fuelCost, (decimal)driverCost, (decimal)maintCost, (decimal)profit);
        total.Should().Be((decimal)expectedTotal);
    }
}
