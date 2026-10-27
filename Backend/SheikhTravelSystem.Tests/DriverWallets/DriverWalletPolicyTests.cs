using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.DriverWallets;

public class DriverWalletPolicyTests
{
    [Fact]
    public void Minimum_cash_out_is_500_pkr()
    {
        DriverWalletPolicy.MinimumCashOutAmountPkr.Should().Be(500m);
        DriverWalletPolicy.MeetsMinimumCashOut(499.99m).Should().BeFalse();
        DriverWalletPolicy.MeetsMinimumCashOut(500m).Should().BeTrue();
    }

    [Fact]
    public void Processing_fee_is_two_percent_of_gross()
    {
        DriverWalletPolicy.CashOutProcessingFeeRate.Should().Be(0.02m);
        var breakdown = DriverWalletPolicy.CalculateFeeBreakdown(1000m);
        breakdown.GrossAmount.Should().Be(1000m);
        breakdown.ProcessingFee.Should().Be(20m);
        breakdown.NetPayout.Should().Be(980m);
    }
}
