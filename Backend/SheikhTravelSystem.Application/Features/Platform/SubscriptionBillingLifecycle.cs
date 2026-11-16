using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Platform;

public static class SubscriptionBillingLifecycle
{
    public const string StatusActive = "Active";
    public const string StatusPastDue = "PastDue";
    public const string StatusExpired = "Expired";
    public const string StatusSuspended = "Suspended";
    public const string StatusCancelled = "Cancelled";

    public static readonly HashSet<string> ModulesAllowedWhenLocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "DASHBOARD",
        "ACCESS"
    };

    public static DateTime? ComputeAccessExpiry(DateTime? paidThroughUtc)
        => paidThroughUtc?.AddDays(SubscriptionPlanPricing.OverdueGraceDays);

    public static string NormalizeInvoiceStatus(
        string? storedStatus,
        decimal amount,
        DateTime? dueDateUtc,
        DateTime? paidDateUtc,
        DateTime asOfUtc)
    {
        if (paidDateUtc.HasValue || string.Equals(storedStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            return "Paid";

        if (string.Equals(storedStatus, "Void", StringComparison.OrdinalIgnoreCase))
            return "Void";

        if (IsOverdue(dueDateUtc, storedStatus, asOfUtc))
            return "Overdue";

        return string.IsNullOrWhiteSpace(storedStatus) ? "Pending" : storedStatus;
    }

    public static bool IsOverdue(DateTime? dueDateUtc, string? storedStatus, DateTime asOfUtc)
    {
        if (string.Equals(storedStatus, "Overdue", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!dueDateUtc.HasValue)
            return false;

        var graceEnd = dueDateUtc.Value.AddDays(SubscriptionPlanPricing.OverdueGraceDays);
        return asOfUtc > graceEnd;
    }

    /// <summary>
    /// Resolve lifecycle status. Admin Suspended/Cancelled win. Else Expired when past access expiry
    /// without paid coverage; PastDue when past paid-through with unpaid invoices; else Active.
    /// </summary>
    public static string ResolveSubscriptionStatus(
        string? storedStatus,
        DateTime? paidThroughUtc,
        DateTime? accessExpiresAtUtc,
        bool hasUnpaidRenewalInvoice,
        DateTime asOfUtc)
    {
        if (string.Equals(storedStatus, StatusSuspended, StringComparison.OrdinalIgnoreCase))
            return StatusSuspended;
        if (string.Equals(storedStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase))
            return StatusCancelled;

        var accessExpiry = accessExpiresAtUtc ?? ComputeAccessExpiry(paidThroughUtc);
        if (accessExpiry.HasValue && asOfUtc > accessExpiry.Value
            && (!paidThroughUtc.HasValue || asOfUtc > paidThroughUtc.Value))
            return StatusExpired;

        if (paidThroughUtc.HasValue && asOfUtc > paidThroughUtc.Value && hasUnpaidRenewalInvoice)
            return StatusPastDue;

        if (string.Equals(storedStatus, StatusExpired, StringComparison.OrdinalIgnoreCase)
            && paidThroughUtc.HasValue && asOfUtc <= paidThroughUtc.Value)
            return StatusActive;

        if (string.Equals(storedStatus, StatusPastDue, StringComparison.OrdinalIgnoreCase)
            && (!paidThroughUtc.HasValue || asOfUtc <= paidThroughUtc.Value))
            return StatusActive;

        return string.IsNullOrWhiteSpace(storedStatus) ? StatusActive : storedStatus;
    }

    public static bool ShouldLockFeatures(
        string resolvedStatus,
        bool hasOverdueOrPendingPastPaidThrough,
        DateTime asOfUtc)
    {
        if (string.Equals(resolvedStatus, StatusSuspended, StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolvedStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolvedStatus, StatusExpired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(resolvedStatus, StatusPastDue, StringComparison.OrdinalIgnoreCase))
            return true;

        return hasOverdueOrPendingPastPaidThrough;
    }

    /// <summary>Hard deactivate tenant when access expiry passed without paid coverage.</summary>
    public static bool ShouldDeactivateTenant(string resolvedStatus, DateTime? accessExpiresAtUtc, DateTime asOfUtc)
    {
        if (string.Equals(resolvedStatus, StatusExpired, StringComparison.OrdinalIgnoreCase))
            return true;

        if (accessExpiresAtUtc.HasValue && asOfUtc > accessExpiresAtUtc.Value
            && !string.Equals(resolvedStatus, StatusSuspended, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolvedStatus, StatusCancelled, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolvedStatus, StatusActive, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool IsModuleAllowedWhenLocked(string moduleCode)
        => ModulesAllowedWhenLocked.Contains(moduleCode);

    public static int RemainingAccessDays(DateTime? accessExpiresAtUtc, DateTime asOfUtc)
    {
        if (!accessExpiresAtUtc.HasValue) return 0;
        var days = (accessExpiresAtUtc.Value.Date - asOfUtc.Date).TotalDays;
        return (int)Math.Floor(days);
    }

    // Legacy overload used by older call sites
    public static bool ShouldLockFeatures(bool subscriptionSuspended, bool hasOverdueInvoice, DateTime asOfUtc)
        => subscriptionSuspended || hasOverdueInvoice;
}
