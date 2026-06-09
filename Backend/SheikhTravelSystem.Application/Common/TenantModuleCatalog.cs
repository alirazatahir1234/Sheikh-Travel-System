namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Maps enterprise module codes (Modules table) to legacy menu entitlement keys (EnabledModulesJson).
/// </summary>
public static class TenantModuleCatalog
{
    public record ModuleDefinition(string Code, string Name, string[] LegacyKeys);

    public static readonly ModuleDefinition[] All =
    [
        new("DASHBOARD", "Dashboard", ["dashboard"]),
        new("FLEET", "Fleet Management", ["vehicles", "drivers", "fuel-logs", "maintenance"]),
        new("GPS", "GPS Tracking", ["gps-tracking"]),
        new("RENTAL", "Vehicle Rental", ["rental"]),
        new("TRAVEL", "Travel Agency", ["bookings", "routes"]),
        new("CRM", "CRM", ["customers"]),
        new("FINANCE", "Finance", ["payments"]),
        new("HR", "HR", ["hr"]),
        new("ANALYTICS", "Analytics Pro", ["reports", "audit-logs"]),
        new("ACCESS", "Access Control", ["users", "driver-allowance-rules"]),
    ];

    public static IReadOnlyList<string> DefaultModuleCodes { get; } =
        ["DASHBOARD", "FLEET", "GPS", "TRAVEL", "CRM", "FINANCE", "ANALYTICS", "ACCESS"];

    public static IReadOnlyList<string> LegacyKeysFromCodes(IEnumerable<string> moduleCodes)
    {
        var codeSet = moduleCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return All
            .Where(m => codeSet.Contains(m.Code))
            .SelectMany(m => m.LegacyKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> CodesFromLegacyKeys(IEnumerable<string> legacyKeys)
    {
        var keySet = legacyKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return All
            .Where(m => m.LegacyKeys.Any(k => keySet.Contains(k)))
            .Select(m => m.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string SerializeLegacyKeys(IEnumerable<string> legacyKeys)
        => System.Text.Json.JsonSerializer.Serialize(legacyKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
}
