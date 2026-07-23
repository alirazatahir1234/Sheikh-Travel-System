using System.Text.Json;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stage 4 Subscription plan catalog seed (migration + API fallback).
/// Aligns with ERP PLAN_DEFINITIONS (Starter / Pro / Enterprise).
/// </summary>
public static class SubscriptionPlanCatalog
{
    public sealed record Entry(
        string SubscriptionCode,
        string DisplayName,
        string Description,
        string PlanType,
        string Status,
        int SortOrder,
        int? DurationMonths,
        bool IsDefault,
        bool Visible,
        string? DocumentationUrl,
        string[] DefaultModuleCodes,
        int? MaxUsers,
        int? MaxVehicles,
        int? MaxDrivers,
        int? MaxBranches,
        int? MaxGpsDevices,
        int? StorageQuotaGb,
        int? AICredits,
        bool GPSEnabled);

    public static IReadOnlyList<Entry> All { get; } =
    [
        new(
            SubscriptionCode: "STARTER",
            DisplayName: "Starter",
            Description: "Core fleet & GPS operations",
            PlanType: "Standard",
            Status: "Active",
            SortOrder: 10,
            DurationMonths: 12,
            IsDefault: false,
            Visible: true,
            DocumentationUrl: null,
            DefaultModuleCodes: ["DASHBOARD", "FLEET", "GPS", "ACCESS"],
            MaxUsers: 10,
            MaxVehicles: 50,
            MaxDrivers: 50,
            MaxBranches: 5,
            MaxGpsDevices: 50,
            StorageQuotaGb: 50,
            AICredits: 1000,
            GPSEnabled: true),
        new(
            SubscriptionCode: "PRO",
            DisplayName: "Pro",
            Description: "Full operations suite",
            PlanType: "Standard",
            Status: "Active",
            SortOrder: 20,
            DurationMonths: 12,
            IsDefault: false,
            Visible: true,
            DocumentationUrl: null,
            DefaultModuleCodes: ["DASHBOARD", "FLEET", "GPS", "RENTAL", "CRM", "FINANCE", "ANALYTICS", "ACCESS"],
            MaxUsers: 50,
            MaxVehicles: 250,
            MaxDrivers: 250,
            MaxBranches: 15,
            MaxGpsDevices: 250,
            StorageQuotaGb: 250,
            AICredits: 10000,
            GPSEnabled: true),
        new(
            SubscriptionCode: "ENTERPRISE",
            DisplayName: "Enterprise",
            Description: "Unlimited — all modules",
            PlanType: "Enterprise",
            Status: "Active",
            SortOrder: 30,
            DurationMonths: 12,
            IsDefault: true,
            Visible: true,
            DocumentationUrl: null,
            DefaultModuleCodes:
            [
                "DASHBOARD", "FLEET", "GPS", "RENTAL", "TRAVEL", "CRM", "FINANCE", "HR", "ANALYTICS", "ACCESS"
            ],
            MaxUsers: null,
            MaxVehicles: null,
            MaxDrivers: null,
            MaxBranches: null,
            MaxGpsDevices: null,
            StorageQuotaGb: null,
            AICredits: null,
            GPSEnabled: true),
    ];

    public static Entry Default => All.First(e => e.IsDefault);

    public static Entry Resolve(string? planNameOrCode)
    {
        if (string.IsNullOrWhiteSpace(planNameOrCode))
            return Default;

        var key = planNameOrCode.Trim();
        return All.FirstOrDefault(e =>
                   string.Equals(e.SubscriptionCode, key, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(e.DisplayName, key, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }

    public static string ToSubscriptionCode(string? planNameOrCode)
        => Resolve(planNameOrCode).SubscriptionCode;

    public static IReadOnlyList<string> LicensedModuleCodes(string? planNameOrCode)
        => Resolve(planNameOrCode).DefaultModuleCodes;

    public static string SerializeModuleCodes(IEnumerable<string> codes)
        => JsonSerializer.Serialize(codes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

    public static IReadOnlyList<string> ParseModuleCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
