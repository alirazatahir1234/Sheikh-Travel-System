namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Paid / premium subscription add-ons — opt-in modules not included in Starter/Pro/Enterprise
/// <see cref="SubscriptionPlanCatalog"/> defaults. Amounts are USD list prices (convert with
/// <see cref="SubscriptionPlanPricing.ToPkr"/> for Pakistan display).
/// </summary>
public static class SubscriptionAddOnCatalog
{
    public sealed record Entry(
        string ModuleCode,
        string DisplayName,
        string Description,
        string Category,
        string[] Dependencies,
        string CurrencyCode,
        decimal? MonthlyAmount,
        int SortOrder);

    public static IReadOnlyList<Entry> All { get; } =
    [
        new(
            ModuleCode: TenantModuleCatalog.AiAssistantModuleCode,
            DisplayName: "AI Document OCR & Smart Fleet Assistant",
            Description: "OCR for registration/fuel receipts and natural-language fleet queries — USD 4 / vehicle / month",
            Category: "Platform",
            Dependencies: ["DASHBOARD"],
            CurrencyCode: SubscriptionPlanPricing.CurrencyUsd,
            MonthlyAmount: 4m,
            SortOrder: 10),
        // SMS / WhatsApp packs ($5 / 250) are metered commercially (see pricing page / docs) — not a TenantModule.
        // Speed / geofence / playback are TenantFeatures (GpsAddOnFeatureCatalog), not module add-ons.
        new(
            ModuleCode: "ALERT_GPS_LOST",
            DisplayName: "GPS Lost",
            Description: "Premium GPS signal lost / antenna fault alerts",
            Category: AlertModuleCatalog.AlertsCategory,
            Dependencies: [AlertModuleCatalog.AlertsModuleCode],
            CurrencyCode: SubscriptionPlanPricing.CurrencyUsd,
            MonthlyAmount: null,
            SortOrder: 22),
        new(
            ModuleCode: "ALERT_LOW_FUEL",
            DisplayName: "Low Fuel",
            Description: "Premium fuel level below configured threshold alerts",
            Category: AlertModuleCatalog.AlertsCategory,
            Dependencies: [AlertModuleCatalog.AlertsModuleCode],
            CurrencyCode: SubscriptionPlanPricing.CurrencyUsd,
            MonthlyAmount: null,
            SortOrder: 23),
    ];

    public static bool IsPremiumAddOn(string? moduleCode)
        => !string.IsNullOrWhiteSpace(moduleCode)
           && (All.Any(e => string.Equals(e.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase))
               || AlertModuleCatalog.IsPremium(moduleCode));

    public static Entry? Resolve(string? moduleCode)
    {
        if (string.IsNullOrWhiteSpace(moduleCode)) return null;
        return All.FirstOrDefault(e =>
            string.Equals(e.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> ModuleCodes
        => All.Select(e => e.ModuleCode).ToList();
}
