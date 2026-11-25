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

    /// <summary>TravelNexus — travel agency, visa and Umrah management. Opt-in; not in defaults.</summary>
    public const string TravelAgencyModuleCode = "TRAVEL_AGENCY";

    /// <summary>Legacy menu entitlement key for <see cref="TravelAgencyModuleCode"/>.</summary>
    public const string TravelAgencyLegacyKey = "travel-agency";

    /// <summary>Major Fleet Alerts module — parent of per-type alert capabilities.</summary>
    public const string AlertsModuleCode = AlertModuleCatalog.AlertsModuleCode;

    /// <summary>
    /// Legacy module code for road-posted speed alerts — now a <see cref="GpsAddOnFeatureCatalog.SpeedAlerts"/>
    /// TenantFeature (kept for migration / backfill lookups).
    /// </summary>
    public const string RoadSpeedAlertsModuleCode = "ROAD_SPEED_ALERTS";

    /// <summary>Core platform module — always-on; not an opt-in subscription toggle.</summary>
    public const string GpsModuleCode = "GPS";

    /// <summary>
    /// Always-on core modules. Forced on every <c>SetTenantModules</c> and shown as non-toggleable in admin UI.
    /// </summary>
    public static IReadOnlyList<string> CoreModuleCodes { get; } = [GpsModuleCode];

    public static bool IsCore(string? moduleCode)
        => !string.IsNullOrWhiteSpace(moduleCode)
           && CoreModuleCodes.Contains(moduleCode, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when <paramref name="featureKey"/> is a TenantModuleCatalog legacy key (or module code).
    /// Used to keep module-level keys out of TenantFeatures soft-gates / feature toggles.
    /// </summary>
    public static bool IsModuleLegacyFeatureKey(string? featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) return false;
        foreach (var module in All)
        {
            if (string.Equals(module.Code, featureKey, StringComparison.OrdinalIgnoreCase))
                return true;
            if (module.LegacyKeys.Any(k =>
                    string.Equals(k, featureKey, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    public static readonly ModuleDefinition[] All =
    [
        new("DASHBOARD", "Dashboard", ["dashboard"]),
        new("FLEET", "Fleet Management", ["vehicles", "drivers", "fuel-logs", "maintenance"]),
        new(GpsModuleCode, "Fleet Tracking", ["gps-tracking"]),
        new("RENTAL", "Vehicle Rental", ["rental"]),
        new(TravelAgencyModuleCode, "TravelNexus", [TravelAgencyLegacyKey]),
        // Roads/routes only — bookings & trips are separate opt-in modules.
        // TRIPS stays catalog-only under BOOKINGS (not listed here → not separately enableable).
        new(TravelRoutesModuleCode, "Routes", ["routes"]),
        new(BookingsModuleCode, "Bookings", ["bookings", "trips"]),
        new("CRM", "CRM", ["customers"]),
        new("FINANCE", "Finance", ["payments"]),
        new("HR", "HR", ["hr"]),
        new("ANALYTICS", "Analytics Pro", ["reports", "audit-logs"]),
        new("ACCESS", "Access Control", ["users", "driver-allowance-rules"]),
        // Major Alerts parent + module-governed per-type children (GPS add-on alert types are TenantFeatures).
        new(AlertsModuleCode, "Fleet Alerts", ["alerts"]),
        ..AlertModuleCatalog.ModuleGovernedTypes.Select(t =>
            new ModuleDefinition(t.ModuleCode, t.DisplayName, [t.LegacyKey])),
        // Premium: SheikhGo AI Assistant — enable only via explicit company opt-in (provision / module management).
        new(AiAssistantModuleCode, "SheikhGo AI Assistant", ["ai"]),
    ];

    /// <summary>
    /// Modules enabled for new tenants when none are specified.
    /// Does <b>not</b> include <see cref="AiAssistantModuleCode"/>, <see cref="BookingsModuleCode"/>,
    /// <see cref="TripsModuleCode"/>, GPS feature add-ons (geofencing / speed-alerts / route-playback),
    /// or premium alert modules (ALERT_GPS_LOST, ALERT_LOW_FUEL).
    /// Includes <see cref="GpsModuleCode"/> (core), <see cref="TravelRoutesModuleCode"/>, and basic
    /// fleet alert types (online/offline, ignition, battery, power cut, SOS).
    /// </summary>
    public static IReadOnlyList<string> DefaultModuleCodes { get; } =
        new[] { "DASHBOARD", "FLEET", GpsModuleCode }
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
