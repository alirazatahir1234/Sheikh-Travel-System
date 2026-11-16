using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;
// SubscriptionBillingCycle lives in Application.Common

namespace SheikhTravelSystem.Tests.Platform;

public class SubscriptionBillingLifecycleTests
{
    [Theory]
    [InlineData("STARTER", 6)]
    [InlineData("Pro", 12)]
    [InlineData("Enterprise", 20)]
    [InlineData(null, 20)]
    public void ResolveMonthlyPerVehicleUsd_uses_list_prices(string? plan, decimal expected)
        => SubscriptionPlanPricing.ResolveMonthlyPerVehicleUsd(plan).Should().Be(expected);

    [Theory]
    [InlineData("STARTER", 61.20)] // 6 * 12 * 0.85
    [InlineData("Pro", 122.40)]
    [InlineData("Enterprise", 204.00)]
    public void ResolveYearlyPerDevice_applies_annual_discount(string plan, decimal expected)
        => SubscriptionPlanPricing.ResolveYearlyPerDevice(plan).Should().Be(expected);

    [Theory]
    [InlineData("Starter", 1, 6)]
    [InlineData("Starter", 3, 18)]
    [InlineData("PRO", 10, 145)] // 12*10 + 25 platform fee
    [InlineData("Enterprise", 25, 500)]
    public void ResolveMonthlyUsd_multiplies_by_seats_and_platform_fee(string plan, int devices, decimal expected)
        => SubscriptionPlanPricing.ResolveMonthlyUsd(plan, devices).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-4)]
    public void ResolveYearlyAmount_bills_at_least_one_vehicle(int? devices)
        => SubscriptionPlanPricing.ResolveYearlyAmount("Starter", devices).Should().Be(61.20m);

    [Fact]
    public void Yearly_cycle_waives_platform_fee_and_applies_discount()
    {
        SubscriptionPlanPricing.ResolveMonthlyAmount("Starter", 1).Should().Be(6m);
        SubscriptionPlanPricing.ResolveCycleAmount("Starter", 1, "Yearly").Should().Be(61.20m);
        SubscriptionPlanPricing.ResolveCycleAmount("Starter", 1, "Monthly").Should().Be(6m);
        SubscriptionPlanPricing.ResolveCycleAmount("PRO", 1, "Monthly").Should().Be(37m); // 12+25
        SubscriptionPlanPricing.ResolveCycleAmount("PRO", 1, "Yearly").Should().Be(122.40m);
    }

    [Fact]
    public void ToPkr_uses_configured_rate()
    {
        var prev = SubscriptionPlanPricing.UsdToPkrRate;
        try
        {
            SubscriptionPlanPricing.UsdToPkrRate = 280m;
            SubscriptionPlanPricing.ToPkr(6m).Should().Be(1680m);
            SubscriptionPlanPricing.ResolveCycleAmountInCurrency("Starter", 1, "Monthly", "PKR")
                .Should().Be(1680m);
        }
        finally
        {
            SubscriptionPlanPricing.UsdToPkrRate = prev;
        }
    }

    [Fact]
    public void ResolveBillingCurrency_detects_pakistan()
    {
        SubscriptionPlanPricing.ResolveBillingCurrency("Pakistan", null).Should().Be("PKR");
        SubscriptionPlanPricing.ResolveBillingCurrency("PK", "USD").Should().Be("PKR");
        SubscriptionPlanPricing.ResolveBillingCurrency("AE", "USD").Should().Be("USD");
        SubscriptionPlanPricing.ResolveBillingCurrency("AE", "PKR").Should().Be("PKR");
    }

    [Fact]
    public void Starter_plan_quotas_match_small_fleet()
    {
        var starter = SubscriptionPlanCatalog.Resolve("Starter");
        starter.MaxUsers.Should().Be(3);
        starter.MaxGpsDevices.Should().Be(1);
        starter.DurationMonths.Should().Be(1);
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

    [Fact]
    public void ComputeAccessExpiry_adds_seven_day_grace()
    {
        var paidThrough = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        SubscriptionBillingLifecycle.ComputeAccessExpiry(paidThrough)
            .Should().Be(paidThrough.AddDays(SubscriptionPlanPricing.OverdueGraceDays));
    }

    [Fact]
    public void ResolveSubscriptionStatus_PastDue_when_past_paid_through_with_unpaid_invoice()
    {
        var paidThrough = DateTime.UtcNow.AddDays(-2);
        var access = SubscriptionBillingLifecycle.ComputeAccessExpiry(paidThrough);
        SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
                "Active", paidThrough, access, hasUnpaidRenewalInvoice: true, DateTime.UtcNow)
            .Should().Be(SubscriptionBillingLifecycle.StatusPastDue);
    }

    [Fact]
    public void ResolveSubscriptionStatus_Expired_after_access_expiry()
    {
        var paidThrough = DateTime.UtcNow.AddDays(-10);
        var access = paidThrough.AddDays(7);
        SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
                "PastDue", paidThrough, access, hasUnpaidRenewalInvoice: true, DateTime.UtcNow)
            .Should().Be(SubscriptionBillingLifecycle.StatusExpired);
    }

    [Fact]
    public void ShouldDeactivateTenant_true_when_Expired()
        => SubscriptionBillingLifecycle.ShouldDeactivateTenant(
                SubscriptionBillingLifecycle.StatusExpired, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow)
            .Should().BeTrue();

    [Fact]
    public void ShouldLockFeatures_for_PastDue_and_Expired()
    {
        SubscriptionBillingLifecycle.ShouldLockFeatures(
            SubscriptionBillingLifecycle.StatusPastDue, false, DateTime.UtcNow).Should().BeTrue();
        SubscriptionBillingLifecycle.ShouldLockFeatures(
            SubscriptionBillingLifecycle.StatusExpired, false, DateTime.UtcNow).Should().BeTrue();
        SubscriptionBillingLifecycle.ShouldLockFeatures(
            SubscriptionBillingLifecycle.StatusActive, false, DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Suspended_wins_over_expiry_clock()
    {
        var paidThrough = DateTime.UtcNow.AddDays(-30);
        SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
                SubscriptionBillingLifecycle.StatusSuspended,
                paidThrough,
                paidThrough.AddDays(7),
                hasUnpaidRenewalInvoice: true,
                DateTime.UtcNow)
            .Should().Be(SubscriptionBillingLifecycle.StatusSuspended);
    }

    [Fact]
    public void RemainingAccessDays_floors_whole_days()
    {
        var asOf = new DateTime(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc);
        var expires = new DateTime(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc);
        SubscriptionBillingLifecycle.RemainingAccessDays(expires, asOf).Should().Be(4);
    }

    [Theory]
    [InlineData(2024, 1, 31, 2024, 2, 29)] // leap Feb clamp
    [InlineData(2025, 1, 31, 2025, 2, 28)]
    [InlineData(2026, 3, 31, 2026, 4, 30)]
    public void PeriodEnd_monthly_clamps_month_end(int y1, int m1, int d1, int y2, int m2, int d2)
        => SubscriptionBillingCycle.PeriodEnd(new DateTime(y1, m1, d1), "Monthly")
            .Should().Be(new DateTime(y2, m2, d2));
}
