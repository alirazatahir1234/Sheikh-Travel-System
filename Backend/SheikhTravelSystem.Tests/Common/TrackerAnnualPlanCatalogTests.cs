using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class TrackerAnnualPlanCatalogTests
{
    [Fact]
    public void Resolve_unknown_code_falls_back_to_default_plus()
    {
        var plan = TrackerAnnualPlanCatalog.Resolve("NOT_A_PLAN");
        Assert.Equal("TRACKER_PLUS", plan.PlanCode);
        Assert.Equal(TrackerAnnualPlanCatalog.PlusAnnualPkr, plan.AnnualAmountPkr);
    }

    [Theory]
    [InlineData("TRACKER_ESSENTIAL", 8_000)]
    [InlineData("TRACKER_PLUS", 9_000)]
    [InlineData("TRACKER_PREMIUM", 10_000)]
    public void Resolve_returns_market_aligned_annual_prices(string code, decimal expected)
    {
        Assert.Equal(expected, TrackerAnnualPlanCatalog.ResolveAnnualAmount(code));
    }

    [Fact]
    public void All_plans_are_single_tracker_annual()
    {
        Assert.Equal(3, TrackerAnnualPlanCatalog.All.Count);
        Assert.All(TrackerAnnualPlanCatalog.All, p => Assert.True(p.AnnualAmountPkr >= 8_000 && p.AnnualAmountPkr <= 10_000));
    }
}
