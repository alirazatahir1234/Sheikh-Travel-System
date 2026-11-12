namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Commercial PKR list prices for SheikhGo Nexus subscription tiers (website + ERP aligned).
/// Every tier is a one-year package priced <b>per licensed GPS device</b>:
/// yearly invoice = per-device price × licensed devices (<c>TenantSubscriptions.MaxGpsDevices</c>, minimum 1).
/// Payment gateway capture is manual until provider credentials are configured — see docs/SUBSCRIPTION_BILLING.md.
/// </summary>
public static class SubscriptionPlanPricing
{
    public const decimal StarterYearlyPerDevicePkr = 20_000m;
    public const decimal ProYearlyPerDevicePkr = 45_000m;
    public const decimal EnterpriseYearlyPerDevicePkr = 70_000m;

    public const int InvoiceDueDays = 14;
    public const int OverdueGraceDays = 7;

    public static decimal ResolveYearlyPerDevice(string? planNameOrCode)
        => SubscriptionPlanCatalog.Resolve(planNameOrCode).SubscriptionCode switch
        {
            "STARTER" => StarterYearlyPerDevicePkr,
            "PRO" => ProYearlyPerDevicePkr,
            _ => EnterpriseYearlyPerDevicePkr
        };

    /// <summary>Devices billed: the licensed count, never less than one.</summary>
    public static int BillableDevices(int? licensedDevices)
        => Math.Max(1, licensedDevices ?? 1);

    /// <summary>Yearly package price for the licensed device count.</summary>
    public static decimal ResolveYearlyAmount(string? planNameOrCode, int? licensedDevices)
        => ResolveYearlyPerDevice(planNameOrCode) * BillableDevices(licensedDevices);

    /// <summary>
    /// Monthly equivalent of the yearly package (yearly ÷ 12, rounded to paisa). Stored in
    /// <c>TenantSubscriptions.MonthlyAmount</c> for MRR reporting and legacy Monthly-cycle tenants.
    /// </summary>
    public static decimal ResolveMonthlyAmount(string? planNameOrCode, int? licensedDevices)
        => Math.Round(ResolveYearlyAmount(planNameOrCode, licensedDevices) / 12m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Invoice amount for one billing period. Yearly bills the exact package price (no ÷12 rounding drift).</summary>
    public static decimal ResolveCycleAmount(string? planNameOrCode, int? licensedDevices, string? billingCycle)
        => SubscriptionBillingCycle.MonthsPerCycle(billingCycle) == 12
            ? ResolveYearlyAmount(planNameOrCode, licensedDevices)
            : ResolveMonthlyAmount(planNameOrCode, licensedDevices);
}
