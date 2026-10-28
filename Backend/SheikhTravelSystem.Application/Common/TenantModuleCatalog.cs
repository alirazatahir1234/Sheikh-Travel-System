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

    /// <summary>Vehicle hire rental — opt-in; depends on fleet vehicles + customers.</summary>
    public const string RentalModuleCode = "RENTAL";

    /// <summary>Major Fleet Alerts module — parent of per-type alert capabilities.</summary>
    public const string AlertsModuleCode = AlertModuleCatalog.AlertsModuleCode;

    /// <summary>
    /// Premium road-posted speed limit overspeed alerts (Google Roads Speed Limits) —
    /// paid subscription add-on under <see cref="AlertsModuleCode"/>; never in <see cref="DefaultModuleCodes"/>.
    /// See <see cref="SubscriptionAddOnCatalog"/> and <see cref="AlertModuleCatalog"/>.
    /// </summary>
    public const string RoadSpeedAlertsModuleCode = "ROAD_SPEED_ALERTS";

    public static readonly ModuleDefinition[] All =
    [
        new("DASHBOARD", "Dashboard", ["dashboard"]),
        new("FLEET", "Fleet Management", ["vehicles", "drivers", "fuel-logs", "maintenance"]),
        new("GPS", "Fleet Tracking", ["gps-tracking"]),
        new("RENTAL", "Vehicle Rental", ["rental"]),
        // Roads/routes only — bookings & trips are separate opt-in modules.
        // TRIPS stays catalog-only under BOOKINGS (not listed here → not separately enableable).
        new(TravelRoutesModuleCode, "Routes", ["routes"]),
        new(BookingsModuleCode, "Bookings", ["bookings", "trips"]),
        new("CRM", "CRM", ["customers"]),
        new("FINANCE", "Finance", ["payments"]),
        new("HR", "HR", ["hr"]),
        new("ANALYTICS", "Analytics Pro", ["reports", "audit-logs"]),
        new("ACCESS", "Access Control", ["users", "driver-allowance-rules"]),
        // Major Alerts parent + per-type children (see AlertModuleCatalog).
        new(AlertsModuleCode, "Fleet Alerts", ["alerts"]),
        ..AlertModuleCatalog.Types.Select(t => new ModuleDefinition(t.ModuleCode, t.DisplayName, [t.LegacyKey])),
        // Premium: SheikhGo AI Assistant — enable only via explicit company opt-in (provision / module management).
        new(AiAssistantModuleCode, "SheikhGo AI Assistant", ["ai"]),
    ];

    /// <summary>
    /// Modules enabled for new tenants when none are specified.
    /// Does <b>not</b> include <see cref="AiAssistantModuleCode"/>, <see cref="BookingsModuleCode"/>,
    /// <see cref="TripsModuleCode"/>, or premium alert types
    /// (<see cref="RoadSpeedAlertsModuleCode"/>, ALERT_OVERSPEED, ALERT_GPS_LOST, ALERT_LOW_FUEL).
    /// Includes <see cref="TravelRoutesModuleCode"/> (roads/routes) and basic fleet alert types
    /// (online/offline, ignition, battery, power cut, geofence, SOS) — always default-on, not premium.
    /// </summary>
    public static IReadOnlyList<string> DefaultModuleCodes { get; } =
        new[] { "DASHBOARD", "FLEET", "GPS" }
            .Concat(AlertModuleCatalog.DefaultModuleCodes)
            .Concat(["TRAVEL", "CRM", "FINANCE", "ANALYTICS", "ACCESS"])
            .ToList();

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
