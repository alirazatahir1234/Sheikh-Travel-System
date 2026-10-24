namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Maps enterprise module codes (Modules table) to legacy menu entitlement keys (EnabledModulesJson).
/// </summary>
public static class TenantModuleCatalog
{
    public record ModuleDefinition(string Code, string Name, string[] LegacyKeys);

    /// <summary>Premium SheikhGo AI Assistant module — opt-in only; never in <see cref="DefaultModuleCodes"/>.</summary>
    public const string AiAssistantModuleCode = "AI";

    /// <summary>Bookings workflows — opt-in; not bundled with routes or trips.</summary>
    public const string BookingsModuleCode = "BOOKINGS";

    /// <summary>Trip lifecycle / dispatch — opt-in; independent of bookings.</summary>
    public const string TripsModuleCode = "TRIPS";

    /// <summary>Roads / routes catalog — independent of bookings/trips.</summary>
    public const string TravelRoutesModuleCode = "TRAVEL";

    public static readonly ModuleDefinition[] All =
    [
        new("DASHBOARD", "Dashboard", ["dashboard"]),
        new("FLEET", "Fleet Management", ["vehicles", "drivers", "fuel-logs", "maintenance"]),
        new("GPS", "Fleet Tracking", ["gps-tracking"]),
        new("RENTAL", "Vehicle Rental", ["rental"]),
        // Roads/routes only — bookings & trips are separate opt-in modules.
        new(TravelRoutesModuleCode, "Routes", ["routes"]),
        new(BookingsModuleCode, "Bookings", ["bookings"]),
        new(TripsModuleCode, "Trips", ["trips"]),
        new("CRM", "CRM", ["customers"]),
        new("FINANCE", "Finance", ["payments"]),
        new("HR", "HR", ["hr"]),
        new("ANALYTICS", "Analytics Pro", ["reports", "audit-logs"]),
        new("ACCESS", "Access Control", ["users", "driver-allowance-rules"]),
        // Premium: SheikhGo AI Assistant — enable only via explicit company opt-in (provision / module management).
        new(AiAssistantModuleCode, "SheikhGo AI Assistant", ["ai"]),
    ];

    /// <summary>
    /// Modules enabled for new tenants when none are specified.
    /// Does <b>not</b> include <see cref="AiAssistantModuleCode"/>, <see cref="BookingsModuleCode"/>,
    /// or <see cref="TripsModuleCode"/> (opt-in).
    /// Includes <see cref="TravelRoutesModuleCode"/> (roads/routes) only.
    /// </summary>
    public static IReadOnlyList<string> DefaultModuleCodes { get; } =
        ["DASHBOARD", "FLEET", "GPS", "TRAVEL", "CRM", "FINANCE", "ANALYTICS", "ACCESS"];

    /// <summary>
    /// Whether a product module code (or legacy key) is covered by the tenant's enabled legacy keys.
    /// Used by menu/dashboard soft gates; empty enabled set is treated as "no filter" by callers.
    /// </summary>
    public static bool IsEntitled(string? moduleCodeOrLegacyKey, IEnumerable<string> enabledLegacyKeys)
    {
        if (string.IsNullOrWhiteSpace(moduleCodeOrLegacyKey))
            return true;

        var enabled = enabledLegacyKeys as ISet<string>
            ?? enabledLegacyKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (enabled.Contains(moduleCodeOrLegacyKey))
            return true;

        var def = All.FirstOrDefault(m =>
            string.Equals(m.Code, moduleCodeOrLegacyKey, StringComparison.OrdinalIgnoreCase));
        if (def is null)
            return false;

        return def.LegacyKeys.Any(k => enabled.Contains(k));
    }

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
