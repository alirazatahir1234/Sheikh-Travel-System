using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class SubscriptionBillingCycleTests
{
    [Theory]
    [InlineData("Monthly", "Monthly")]
    [InlineData("yearly", "Yearly")]
    [InlineData("Yearly", "Yearly")]
    [InlineData("annual", "Yearly")]
    public void TryNormalize_accepts_aliases(string input, string expected)
        => SubscriptionBillingCycle.TryNormalize(input).Should().Be(expected);

    [Fact]
    public void Default_IsMonthly()
        => SubscriptionBillingCycle.Default.Should().Be(SubscriptionBillingCycle.Monthly);

    [Fact]
    public void PeriodEnd_Yearly_AddsTwelveMonths()
        => SubscriptionBillingCycle.PeriodEnd(new DateTime(2026, 11, 12), "Yearly")
            .Should().Be(new DateTime(2027, 11, 12));

    [Fact]
    public void PeriodEnd_Monthly_AddsOneMonth()
        => SubscriptionBillingCycle.PeriodEnd(new DateTime(2026, 11, 12), "Monthly")
            .Should().Be(new DateTime(2026, 12, 12));
}
