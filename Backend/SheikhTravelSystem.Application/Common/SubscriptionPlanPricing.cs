namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Commercial PKR list prices for SheikhGo Nexus subscription tiers (website + ERP aligned).
/// Payment gateway capture is manual until provider credentials are configured — see docs/SUBSCRIPTION_BILLING.md.
/// </summary>
public static class SubscriptionPlanPricing
{
    public const decimal StarterMonthlyPkr = 79_000m;
    public const decimal ProMonthlyPkr = 119_000m;
    public const decimal EnterpriseBaseMonthlyPkr = 165_000m;
    public const int EnterpriseIncludedVehicles = 50;
    public const decimal EnterprisePerExtraVehiclePkr = 2_200m;

    public const int InvoiceDueDays = 14;
    public const int OverdueGraceDays = 7;

    public static decimal ResolveMonthlyAmount(string? planNameOrCode, int activeVehicleCount = 0)
    {
        var plan = SubscriptionPlanCatalog.Resolve(planNameOrCode);
        return plan.SubscriptionCode switch
        {
            "STARTER" => StarterMonthlyPkr,
            "PRO" => ProMonthlyPkr,
            "ENTERPRISE" => ComputeEnterpriseMonthly(activeVehicleCount),
            _ => EnterpriseBaseMonthlyPkr
        };
    }

    public static decimal ComputeEnterpriseMonthly(int activeVehicleCount)
    {
        if (activeVehicleCount <= EnterpriseIncludedVehicles)
            return EnterpriseBaseMonthlyPkr;

        var extra = activeVehicleCount - EnterpriseIncludedVehicles;
        return EnterpriseBaseMonthlyPkr + extra * EnterprisePerExtraVehiclePkr;
    }
}
