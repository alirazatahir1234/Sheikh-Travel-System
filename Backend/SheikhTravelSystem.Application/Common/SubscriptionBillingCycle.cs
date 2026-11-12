namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Billing-cycle rules for Nexus subscriptions. All plans are sold as one-year packages; Monthly is kept
/// only for legacy tenants. Amounts come from <see cref="SubscriptionPlanPricing.ResolveCycleAmount"/>.
/// </summary>
public static class SubscriptionBillingCycle
{
    public const string Monthly = "Monthly";
    public const string Yearly = "Yearly";

    /// <summary>Cycle applied to newly provisioned companies.</summary>
    public const string Default = Yearly;

    public static bool IsValid(string? cycle) => TryNormalize(cycle) is not null;

    /// <summary>Returns the canonical cycle name, or null when the value is not recognised.</summary>
    public static string? TryNormalize(string? cycle)
    {
        var value = cycle?.Trim();
        if (string.IsNullOrEmpty(value)) return null;

        if (value.Equals(Monthly, StringComparison.OrdinalIgnoreCase))
            return Monthly;

        if (value.Equals(Yearly, StringComparison.OrdinalIgnoreCase)
            || value.Equals("Annual", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Annually", StringComparison.OrdinalIgnoreCase))
            return Yearly;

        return null;
    }

    /// <summary>Canonical cycle name; unknown or empty values fall back to <paramref name="fallback"/>.</summary>
    public static string Normalize(string? cycle, string fallback = Monthly)
        => TryNormalize(cycle) ?? fallback;

    public static int MonthsPerCycle(string? cycle)
        => Normalize(cycle) == Yearly ? 12 : 1;

    /// <summary>End of a subscription period that starts on <paramref name="periodStart"/>.</summary>
    public static DateTime PeriodEnd(DateTime periodStart, string? cycle)
        => periodStart.AddMonths(MonthsPerCycle(cycle));
}
