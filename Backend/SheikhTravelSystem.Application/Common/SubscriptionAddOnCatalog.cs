namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Paid / premium subscription add-ons — opt-in modules not included in Starter/Pro/Enterprise
/// <see cref="SubscriptionPlanCatalog"/> defaults. Tenants "subscribe" by enabling the module
/// (Platform Module Management / Company Profile Modules). No invented list prices —
/// <see cref="Entry.MonthlyAmount"/> stays null until commercial pricing is configured.
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
            DisplayName: "SheikhGo AI Assistant",
            Description: "Premium SheikhGo AI Assistant for operations chat and tools",
            Category: "Platform",
            Dependencies: ["DASHBOARD"],
            CurrencyCode: "PKR",
            MonthlyAmount: null,
            SortOrder: 10),
        new(
            ModuleCode: TenantModuleCatalog.RoadSpeedAlertsModuleCode,
            DisplayName: "Road Speed Alerts",
            Description: "Premium overspeed alerts using posted road speed limits (Google Roads Speed Limits)",
            Category: AlertModuleCatalog.AlertsCategory,
            Dependencies: [AlertModuleCatalog.AlertsModuleCode],
            CurrencyCode: "PKR",
            MonthlyAmount: null,
            SortOrder: 20),
        new(
            ModuleCode: "ALERT_OVERSPEED",
            DisplayName: "Fixed Speed Limit",
            Description: "Premium GPS fixed speed-limit overspeed alerts (GpsAlertRules)",
            Category: AlertModuleCatalog.AlertsCategory,
            Dependencies: [AlertModuleCatalog.AlertsModuleCode],
            CurrencyCode: "PKR",
            MonthlyAmount: null,
            SortOrder: 21),
        new(
            ModuleCode: "ALERT_GPS_LOST",
            DisplayName: "GPS Lost",
            Description: "Premium GPS signal lost / antenna fault alerts",
            Category: AlertModuleCatalog.AlertsCategory,
            Dependencies: [AlertModuleCatalog.AlertsModuleCode],
            CurrencyCode: "PKR",
            MonthlyAmount: null,
            SortOrder: 22),
        new(
            ModuleCode: "ALERT_LOW_FUEL",
            DisplayName: "Low Fuel",
            Description: "Premium fuel level below configured threshold alerts",
            Category: AlertModuleCatalog.AlertsCategory,
            Dependencies: [AlertModuleCatalog.AlertsModuleCode],
            CurrencyCode: "PKR",
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
