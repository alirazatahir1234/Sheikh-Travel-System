namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Fleet alert-type modules under the major <see cref="AlertsModuleCode"/> parent.
/// Maps GpsAlertEvents / notification event types → TenantModules codes.
/// Only inventories types that already exist in the platform (see <c>AlertTypeCatalog</c>).
/// </summary>
public static class AlertModuleCatalog
{
    public const string AlertsModuleCode = "ALERTS";
    public const string AlertsCategory = "Alerts";

    /// <summary>Premium: road-posted speed limit (Google Roads) — opt-in; never in defaults.</summary>
    public const string RoadSpeedAlertsModuleCode = TenantModuleCatalog.RoadSpeedAlertsModuleCode;

    public sealed record AlertTypeDefinition(
        string ModuleCode,
        string EventType,
        string Name,
        string DisplayName,
        string Description,
        string Icon,
        string LegacyKey,
        int SortOrder,
        bool IsPremium,
        bool DefaultEnabled,
        string[] EventTypeAliases);

    /// <summary>
    /// Alert-type child modules (excluding the <see cref="AlertsModuleCode"/> parent).
    /// EventType is the canonical AlertTypeCatalog / settings-matrix spelling.
    /// </summary>
    public static readonly AlertTypeDefinition[] Types =
    [
        // Basic (standard plan): no premium tag; DefaultEnabled for all GPS/ALERTS tenants.
        new("ALERT_SOS", "sos", "SOS", "SOS / Panic",
            "SOS and panic alarm alerts from trackers", "emergency", "alert-sos", 201,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["sos"]),
        new("ALERT_POWER_CUT", "power_cut", "Power Cut", "Power Cut",
            "Device main-power cut / disconnect alerts", "power_off", "alert-power-cut", 202,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["power_cut"]),
        // Premium: fixed speed limit — opt-in; never in plan/tenant defaults.
        new("ALERT_OVERSPEED", "overspeed", "Fixed Speed", "Fixed Speed Limit",
            "Premium GPS fixed speed-limit overspeed alerts (GpsAlertRules)", "speed", "alert-overspeed", 203,
            IsPremium: true, DefaultEnabled: false, EventTypeAliases: ["overspeed", "speed_exceeded"]),
        new(RoadSpeedAlertsModuleCode, "road_speed_exceeded", "Road Speed Alerts", "Road Speed Alerts",
            "Premium overspeed alerts using posted road speed limits (Google Roads Speed Limits)",
            "speed", "road-speed-alerts", 204,
            IsPremium: true, DefaultEnabled: false, EventTypeAliases: ["road_speed_exceeded"]),
        new("ALERT_GEOFENCE_ENTER", "geofence_enter", "Geofence Enter", "Geofence Enter",
            "Vehicle entered a watched geofence", "login", "alert-geofence-enter", 205,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["geofence_enter"]),
        new("ALERT_GEOFENCE_EXIT", "geofence_exit", "Geofence Exit", "Geofence Exit",
            "Vehicle exited a watched geofence", "logout", "alert-geofence-exit", 206,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["geofence_exit"]),
        new("ALERT_OFFLINE", "offline", "Vehicle Offline", "Vehicle Offline",
            "Tracker offline / no-GPRS (includes Gmeet SOS-number SMS)", "cloud_off", "alert-offline", 207,
            IsPremium: false, DefaultEnabled: true,
            EventTypeAliases: ["offline", "vehicle_offline", "device_offline", "gps_offline"]),
        new("ALERT_ONLINE", "online", "Vehicle Online", "Vehicle Online",
            "Tracker came back online", "cloud_done", "alert-online", 208,
            IsPremium: false, DefaultEnabled: true,
            EventTypeAliases: ["online", "vehicle_online", "device_online", "gps_online"]),
        new("ALERT_IGNITION_ON", "ignition_on", "Ignition ON", "Ignition ON",
            "ACC / ignition turned ON (SMS via notification pipeline)", "toggle_on", "alert-ignition-on", 209,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["ignition_on"]),
        new("ALERT_IGNITION_OFF", "ignition_off", "Ignition OFF", "Ignition OFF",
            "ACC / ignition turned OFF (SMS via notification pipeline)", "toggle_off", "alert-ignition-off", 210,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["ignition_off"]),
        new("ALERT_LOW_BATTERY", "low_battery", "Low Battery", "Low Battery",
            "Device backup battery low", "battery_alert", "alert-low-battery", 211,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["low_battery"]),
        // Premium: GPS lost / low fuel — opt-in; never in plan/tenant defaults.
        new("ALERT_GPS_LOST", "gps_lost", "GPS Lost", "GPS Lost",
            "Premium GPS signal lost / antenna fault alerts", "gps_off", "alert-gps-lost", 212,
            IsPremium: true, DefaultEnabled: false, EventTypeAliases: ["gps_lost"]),
        new("ALERT_LOW_FUEL", "low_fuel", "Low Fuel", "Low Fuel",
            "Premium fuel level below configured threshold alerts", "local_gas_station", "alert-low-fuel", 213,
            IsPremium: true, DefaultEnabled: false, EventTypeAliases: ["low_fuel"]),
        new("ALERT_FUEL_STATION_STOP", "fuel_station_stop", "Fuel Station Stop", "Fuel Station Stop",
            "Vehicle stopped near a petrol station (GPS stop + Places POI proximity)", "local_gas_station",
            "alert-fuel-station-stop", 214,
            IsPremium: false, DefaultEnabled: true, EventTypeAliases: ["fuel_station_stop"]),
    ];

    /// <summary>Parent + default-on children (excludes premium opt-in types).</summary>
    public static IReadOnlyList<string> DefaultModuleCodes { get; } =
        new[] { AlertsModuleCode }
            .Concat(Types.Where(t => t.DefaultEnabled).Select(t => t.ModuleCode))
            .ToList();

    public static IReadOnlyList<string> PremiumModuleCodes { get; } =
        Types.Where(t => t.IsPremium).Select(t => t.ModuleCode).ToList();

    public static IReadOnlyList<string> AllModuleCodes { get; } =
        new[] { AlertsModuleCode }
            .Concat(Types.Select(t => t.ModuleCode))
            .ToList();

    public static bool IsPremium(string? moduleCode)
        => !string.IsNullOrWhiteSpace(moduleCode)
           && PremiumModuleCodes.Contains(moduleCode, StringComparer.OrdinalIgnoreCase);

    public static bool IsAlertModule(string? moduleCode)
        => !string.IsNullOrWhiteSpace(moduleCode)
           && AllModuleCodes.Contains(moduleCode, StringComparer.OrdinalIgnoreCase);

    public static AlertTypeDefinition? FindByModuleCode(string? moduleCode)
        => Types.FirstOrDefault(t =>
            string.Equals(t.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase));

    public static AlertTypeDefinition? FindByEventType(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return null;

        return Types.FirstOrDefault(t =>
            t.EventTypeAliases.Any(a =>
                string.Equals(a, eventType, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Module code that must be enabled for this event type, or null when the event is
    /// not part of the Alerts module inventory (e.g. maintenance / telematics-only types).
    /// </summary>
    public static string? ModuleCodeForEventType(string? eventType)
        => FindByEventType(eventType)?.ModuleCode;
}
