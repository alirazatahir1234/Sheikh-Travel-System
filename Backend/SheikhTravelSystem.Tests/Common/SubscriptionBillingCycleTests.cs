using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class SubscriptionBillingCycleTests
{
    [Theory]
    [InlineData("Yearly", "Yearly")]
    [InlineData("annual", "Yearly")]
    [InlineData(" MONTHLY ", "Monthly")]
    [InlineData("Weekly", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TryNormalize_MapsKnownCycles(string? input, string? expected)
        => SubscriptionBillingCycle.TryNormalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_UnknownCycle_FallsBackToMonthly()
        => SubscriptionBillingCycle.Normalize("bogus").Should().Be(SubscriptionBillingCycle.Monthly);

    [Fact]
    public void Default_IsYearly()
        => SubscriptionBillingCycle.Default.Should().Be(SubscriptionBillingCycle.Yearly);

    [Fact]
    public void PeriodEnd_Yearly_AddsTwelveMonths()
        => SubscriptionBillingCycle.PeriodEnd(new DateTime(2026, 11, 12), "Yearly")
            .Should().Be(new DateTime(2027, 11, 12));

    [Fact]
    public void PeriodEnd_Monthly_AddsOneMonth()
        => SubscriptionBillingCycle.PeriodEnd(new DateTime(2026, 1, 31), "Monthly")
            .Should().Be(new DateTime(2026, 2, 28));
}
