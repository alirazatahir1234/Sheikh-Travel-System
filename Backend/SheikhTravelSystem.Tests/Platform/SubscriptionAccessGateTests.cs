using FluentAssertions;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.API.Middleware;

namespace SheikhTravelSystem.Tests.Platform;

/// <summary>
/// Documents auth / JWT hard-gate contract codes used by login, refresh, and middleware.
/// </summary>
public class SubscriptionAccessGateTests
{
    [Fact]
    public void Middleware_exposes_stable_error_codes()
    {
        TenantSubscriptionAccessMiddleware.CodeTenantDeactivated.Should().Be("tenant_deactivated");
        TenantSubscriptionAccessMiddleware.CodeSubscriptionExpired.Should().Be("subscription_expired");
    }

    [Fact]
    public void Invoice_create_must_not_be_confused_with_paid_through_extension()
    {
        // Contract: PaidThrough advances only on mark-paid; AccessExpires = PaidThrough + grace.
        var paidThrough = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var access = SubscriptionBillingLifecycle.ComputeAccessExpiry(paidThrough)!.Value;

        // After invoice create (unpaid), still PastDue once past paid-through — not Active via invoice alone.
        var asOf = paidThrough.AddDays(1);
        SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
                "Active", paidThrough, access, hasUnpaidRenewalInvoice: true, asOf)
            .Should().Be(SubscriptionBillingLifecycle.StatusPastDue);

        // After access expiry without payment → Expired (login/JWT must reject).
        var afterGrace = access.AddMinutes(1);
        SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
                "PastDue", paidThrough, access, hasUnpaidRenewalInvoice: true, afterGrace)
            .Should().Be(SubscriptionBillingLifecycle.StatusExpired);
        SubscriptionBillingLifecycle.ShouldDeactivateTenant(
                SubscriptionBillingLifecycle.StatusExpired, access, afterGrace)
            .Should().BeTrue();
    }

    [Fact]
    public void Mark_paid_recovery_resolves_Active_when_paid_through_covers_now()
    {
        var paidThrough = DateTime.UtcNow.AddMonths(1);
        var access = SubscriptionBillingLifecycle.ComputeAccessExpiry(paidThrough);
        SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
                "Expired", paidThrough, access, hasUnpaidRenewalInvoice: false, DateTime.UtcNow)
            .Should().Be(SubscriptionBillingLifecycle.StatusActive);
    }

    [Fact]
    public void AutoRenew_off_still_expires_on_access_clock()
    {
        // AutoRenew=0 skips invoice creation in repository; expiry still uses paid-through + grace.
        var paidThrough = DateTime.UtcNow.AddDays(-10);
        var access = paidThrough.AddDays(7);
        SubscriptionBillingLifecycle.ShouldDeactivateTenant(
                SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
                    "Active", paidThrough, access, hasUnpaidRenewalInvoice: false, DateTime.UtcNow),
                access,
                DateTime.UtcNow)
            .Should().BeTrue();
    }
}
