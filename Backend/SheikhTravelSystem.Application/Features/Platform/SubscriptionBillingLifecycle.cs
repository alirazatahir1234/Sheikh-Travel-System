using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Platform;

public static class SubscriptionBillingLifecycle
{
    public static readonly HashSet<string> ModulesAllowedWhenLocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "DASHBOARD",
        "ACCESS"
    };

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

    public static bool ShouldLockFeatures(bool subscriptionSuspended, bool hasOverdueInvoice, DateTime asOfUtc)
    {
        if (subscriptionSuspended)
            return true;

        return hasOverdueInvoice;
    }

    public static bool IsModuleAllowedWhenLocked(string moduleCode)
        => ModulesAllowedWhenLocked.Contains(moduleCode);
}
