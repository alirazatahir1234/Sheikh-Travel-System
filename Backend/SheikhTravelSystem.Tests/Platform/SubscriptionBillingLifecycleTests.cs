using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Platform;

public class SubscriptionBillingLifecycleTests
{
    [Theory]
    [InlineData("STARTER", 0, 79_000)]
    [InlineData("Pro", 0, 119_000)]
    [InlineData("Enterprise", 40, 165_000)]
    [InlineData("ENTERPRISE", 55, 176_000)]
    public void ResolveMonthlyAmount_uses_list_prices(string plan, int vehicles, decimal expected)
        => SubscriptionPlanPricing.ResolveMonthlyAmount(plan, vehicles).Should().Be(expected);

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
