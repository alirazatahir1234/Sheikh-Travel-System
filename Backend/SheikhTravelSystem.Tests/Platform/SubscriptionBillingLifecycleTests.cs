using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Platform;

public class SubscriptionBillingLifecycleTests
{
    [Theory]
    [InlineData("STARTER", 20_000)]
    [InlineData("Pro", 45_000)]
    [InlineData("Enterprise", 70_000)]
    [InlineData(null, 70_000)]
    public void ResolveYearlyPerDevice_uses_list_prices(string? plan, decimal expected)
        => SubscriptionPlanPricing.ResolveYearlyPerDevice(plan).Should().Be(expected);

    [Theory]
    [InlineData("Starter", 1, 20_000)]
    [InlineData("Starter", 3, 60_000)]
    [InlineData("PRO", 10, 450_000)]
    [InlineData("Enterprise", 25, 1_750_000)]
    public void ResolveYearlyAmount_multiplies_by_licensed_devices(string plan, int devices, decimal expected)
        => SubscriptionPlanPricing.ResolveYearlyAmount(plan, devices).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-4)]
    public void ResolveYearlyAmount_bills_at_least_one_device(int? devices)
        => SubscriptionPlanPricing.ResolveYearlyAmount("Starter", devices).Should().Be(20_000);

    [Fact]
    public void Yearly_cycle_bills_exact_package_without_rounding_drift()
    {
        SubscriptionPlanPricing.ResolveMonthlyAmount("Starter", 1).Should().Be(1_666.67m);
        SubscriptionPlanPricing.ResolveCycleAmount("Starter", 1, "Yearly").Should().Be(20_000);
        SubscriptionPlanPricing.ResolveCycleAmount("Starter", 1, "Monthly").Should().Be(1_666.67m);
    }

    [Fact]
    public void Starter_plan_is_one_user_one_device()
    {
        var starter = SubscriptionPlanCatalog.Resolve("Starter");
        starter.MaxUsers.Should().Be(1);
        starter.MaxGpsDevices.Should().Be(1);
        starter.DurationMonths.Should().Be(12);
    }

    [Fact]
    public void NormalizeInvoiceStatus_marks_overdue_after_grace()
    {
        var due = DateTime.UtcNow.AddDays(-10);
        SubscriptionBillingLifecycle.NormalizeInvoiceStatus("Pending", 1000, due, null, DateTime.UtcNow)
            .Should().Be("Overdue");
    }

    [Fact]
    public void Locked_tenant_still_allows_dashboard_module()
        => SubscriptionBillingLifecycle.IsModuleAllowedWhenLocked("DASHBOARD").Should().BeTrue();
}
