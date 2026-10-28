using FluentAssertions;
using SheikhTravelSystem.Application.Features.Rentals;
using SheikhTravelSystem.Application.Features.Rentals.DTOs;

namespace SheikhTravelSystem.Tests.Rentals;

public class RentalPricingTests
{
    [Fact]
    public void CalculateTotal_Hourly_UsesCeilingHours()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2.2);
        RentalPricing.CalculateTotal(RentalRateTypes.Hourly, 100m, start, end).Should().Be(300m);
        RentalPricing.BillableUnits(RentalRateTypes.Hourly, start, end).Should().Be(3);
    }

    [Fact]
    public void CalculateTotal_Daily_UsesCeilingDays()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(30);
        RentalPricing.CalculateTotal(RentalRateTypes.Daily, 1000m, start, end).Should().Be(2000m);
    }

    [Fact]
    public void CalculateTotal_Weekly_UsesCeilingWeeks()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(8);
        RentalPricing.CalculateTotal(RentalRateTypes.Weekly, 5000m, start, end).Should().Be(10000m);
    }
}
