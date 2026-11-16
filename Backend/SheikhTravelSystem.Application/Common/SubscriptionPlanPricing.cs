namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Commercial USD list prices for SheikhGo Nexus (website + ERP aligned).
/// Every tier is priced <b>per licensed vehicle / GPS seat</b> per month
/// (<c>TenantSubscriptions.MaxGpsDevices</c>, minimum 1).
/// Annual prepaid = monthly × 12 × (1 − <see cref="AnnualDiscountPercent"/>/100); Pro platform fee waived on yearly.
/// Pakistan market invoices/display convert via <see cref="DefaultUsdToPkr"/>.
/// Payment gateway capture is manual until provider credentials are configured — see docs/SUBSCRIPTION_BILLING.md.
/// </summary>
public static class SubscriptionPlanPricing
{
    public const decimal StarterMonthlyPerVehicleUsd = 6m;
    public const decimal ProMonthlyPerVehicleUsd = 12m;
    public const decimal EnterpriseMonthlyPerVehicleUsd = 20m;

    /// <summary>Pro base platform fee (monthly cycle only; waived on annual).</summary>
    public const decimal ProPlatformFeeMonthlyUsd = 25m;

    public const decimal AnnualDiscountPercent = 15m;

    /// <summary>Fallback FX when config is unavailable (ops override via SubscriptionBilling:UsdToPkr).</summary>
    public const decimal DefaultUsdToPkr = 280m;

    public const string CurrencyUsd = "USD";
    public const string CurrencyPkr = "PKR";

    public const int InvoiceDueDays = 14;
    public const int OverdueGraceDays = 7;

    /// <summary>Configurable FX; defaults to <see cref="DefaultUsdToPkr"/>.</summary>
    public static decimal UsdToPkrRate { get; set; } = DefaultUsdToPkr;

    public static decimal ResolveMonthlyPerVehicleUsd(string? planNameOrCode)
        => SubscriptionPlanCatalog.Resolve(planNameOrCode).SubscriptionCode switch
        {
            "STARTER" => StarterMonthlyPerVehicleUsd,
            "PRO" => ProMonthlyPerVehicleUsd,
            _ => EnterpriseMonthlyPerVehicleUsd
        };

    /// <summary>
    /// Legacy name kept for DTOs: yearly prepaid USD per vehicle (15% off; no platform fee).
    /// </summary>
    public static decimal ResolveYearlyPerDevice(string? planNameOrCode)
        => Math.Round(
            ResolveMonthlyPerVehicleUsd(planNameOrCode) * 12m * (1m - AnnualDiscountPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);

    /// <summary>Vehicles billed: the licensed seat count, never less than one.</summary>
    public static int BillableDevices(int? licensedDevices)
        => Math.Max(1, licensedDevices ?? 1);

    public static decimal PlatformFeeMonthlyUsd(string? planNameOrCode, string? billingCycle)
    {
        if (SubscriptionBillingCycle.MonthsPerCycle(billingCycle) == 12)
            return 0m;

        return SubscriptionPlanCatalog.Resolve(planNameOrCode).SubscriptionCode == "PRO"
            ? ProPlatformFeeMonthlyUsd
            : 0m;
    }

    /// <summary>USD for one calendar month (vehicle seats + Pro platform fee when monthly).</summary>
    public static decimal ResolveMonthlyUsd(string? planNameOrCode, int? licensedDevices, string? billingCycle = SubscriptionBillingCycle.Monthly)
    {
        var seats = BillableDevices(licensedDevices);
        var vehicles = ResolveMonthlyPerVehicleUsd(planNameOrCode) * seats;
        return Math.Round(vehicles + PlatformFeeMonthlyUsd(planNameOrCode, billingCycle), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>USD prepaid yearly package (15% off vehicle seats; platform fee waived).</summary>
    public static decimal ResolveYearlyUsd(string? planNameOrCode, int? licensedDevices)
    {
        var seats = BillableDevices(licensedDevices);
        var yearlyPer = ResolveYearlyPerDevice(planNameOrCode);
        return Math.Round(yearlyPer * seats, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Amount stored on <c>TenantSubscriptions.MonthlyAmount</c> / used for MRR:
    /// monthly USD list (including Pro platform fee for monthly-oriented reporting).
    /// </summary>
    public static decimal ResolveMonthlyAmount(string? planNameOrCode, int? licensedDevices)
        => ResolveMonthlyUsd(planNameOrCode, licensedDevices, SubscriptionBillingCycle.Monthly);

    /// <summary>Yearly prepaid USD for the licensed seat count.</summary>
    public static decimal ResolveYearlyAmount(string? planNameOrCode, int? licensedDevices)
        => ResolveYearlyUsd(planNameOrCode, licensedDevices);

    /// <summary>Invoice amount for one billing period in USD.</summary>
    public static decimal ResolveCycleAmount(string? planNameOrCode, int? licensedDevices, string? billingCycle)
        => SubscriptionBillingCycle.MonthsPerCycle(billingCycle) == 12
            ? ResolveYearlyUsd(planNameOrCode, licensedDevices)
            : ResolveMonthlyUsd(planNameOrCode, licensedDevices, billingCycle);

    public static decimal ToPkr(decimal usd, decimal? rate = null)
        => Math.Round(usd * (rate is > 0 ? rate.Value : UsdToPkrRate), 2, MidpointRounding.AwayFromZero);

    public static decimal ResolveCycleAmountInCurrency(
        string? planNameOrCode,
        int? licensedDevices,
        string? billingCycle,
        string? currencyCode)
    {
        var usd = ResolveCycleAmount(planNameOrCode, licensedDevices, billingCycle);
        return IsPkr(currencyCode) ? ToPkr(usd) : usd;
    }

    public static decimal ResolveMonthlyAmountInCurrency(
        string? planNameOrCode,
        int? licensedDevices,
        string? currencyCode)
    {
        var usd = ResolveMonthlyAmount(planNameOrCode, licensedDevices);
        return IsPkr(currencyCode) ? ToPkr(usd) : usd;
    }

    public static bool IsPkr(string? currencyCode)
        => string.Equals(currencyCode?.Trim(), CurrencyPkr, StringComparison.OrdinalIgnoreCase);

    /// <summary>Pakistan market when country/currency hints PK.</summary>
    public static string ResolveBillingCurrency(string? country, string? currencyCode)
    {
        if (IsPkr(currencyCode)) return CurrencyPkr;
        var c = country?.Trim() ?? "";
        if (c.Equals("PK", StringComparison.OrdinalIgnoreCase)
            || c.Equals("PAK", StringComparison.OrdinalIgnoreCase)
            || c.Contains("Pakistan", StringComparison.OrdinalIgnoreCase))
            return CurrencyPkr;
        return CurrencyUsd;
    }
}
