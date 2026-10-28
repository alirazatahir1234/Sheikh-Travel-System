using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Maps Traccar report event types → SheikhGo GpsAlertEvents.EventType spellings
/// (AlertTypeCatalog / AlertModuleCatalog). Skips noise that is not a fleet alert.
/// </summary>
public static class TraccarAlertEventMapper
{
    private static readonly HashSet<string> NoiseTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "deviceMoving",
        "deviceStopped",
        "deviceUnknown",
        "deviceInactive",
        "queuedCommandSent",
        "commandResult",
        "deviceFuelDrop",
        "deviceFuelIncrease",
        "maintenance",
        "textMessage",
        "driverChanged",
        "media"
    };

    /// <summary>
    /// Returns canonical EventType, or null when the Traccar type should not create a GpsAlertEvent.
    /// </summary>
    public static string? MapEventType(string? traccarType, string? alarm = null)
    {
        if (string.IsNullOrWhiteSpace(traccarType))
            return null;

        var raw = traccarType.Trim();
        if (NoiseTypes.Contains(raw))
            return null;

        var harsh = TraccarHarshEventMapper.TryMapCanonical(raw, alarm);
        if (harsh is not null)
            return harsh;

        return raw switch
        {
            "deviceOnline" => "online",
            "deviceOffline" => "vehicle_offline",
            "geofenceEnter" => "geofence_enter",
            "geofenceExit" => "geofence_exit",
            "ignitionOn" => "ignition_on",
            "ignitionOff" => "ignition_off",
            "alarm" => MapAlarmType(alarm),
            // Already-canonical / camelCase collapse for older rows reprocessed via API shape
            "ignitionon" => "ignition_on",
            "ignitionoff" => "ignition_off",
            "geofenceenter" => "geofence_enter",
            "geofenceexit" => "geofence_exit",
            "deviceonline" => "online",
            "deviceoffline" => "vehicle_offline",
            _ => GpsEventTypeNormalizer.Normalize(raw.Contains('_')
                ? raw.ToLowerInvariant()
                : CamelToSnake(raw))
        };
    }

    public static string FormatMessage(string eventType) => eventType switch
    {
        "online" or "device_online" or "vehicle_online" or "gps_online" =>
            "Vehicle has reconnected to the tracking server.",
        "vehicle_offline" or "offline" or "device_offline" or "gps_offline" =>
            "No GPS data received. Vehicle has stopped reporting.",
        "geofence_enter" => "Entered geofence",
        "geofence_exit" => "Exited geofence",
        "ignition_on" => "Ignition turned ON",
        "ignition_off" => "Ignition turned OFF",
        "sos" => "SOS / panic alarm triggered",
        "power_cut" => "External power disconnected",
        "gps_lost" => "GPS signal lost",
        "harsh_braking" => "Harsh braking detected",
        "harsh_acceleration" => "Harsh acceleration detected",
        "alarm" => "Device alarm",
        _ => $"Traccar event: {eventType}"
    };

    /// <summary>
    /// EventType values that match a UI/API filter chip (canonical + aliases + Traccar misspellings).
    /// </summary>
    public static IReadOnlyList<string> ExpandFilterAliases(string? requestedType)
    {
        if (string.IsNullOrWhiteSpace(requestedType))
            return [];

        var key = requestedType.Trim();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key };

        var def = AlertModuleCatalog.FindByEventType(key)
                  ?? AlertModuleCatalog.FindByEventType(GpsEventTypeNormalizer.Normalize(key));
        if (def is not null)
        {
            set.Add(def.EventType);
            foreach (var a in def.EventTypeAliases)
                set.Add(a);
        }

        // Historical Traccar ToLowerInvariant misspellings (pre-mapping fix)
        foreach (var extra in key.ToLowerInvariant() switch
        {
            "ignition_on" or "ignitionon" => new[] { "ignition_on", "ignitionon" },
            "ignition_off" or "ignitionoff" => new[] { "ignition_off", "ignitionoff" },
            "geofence_enter" or "geofenceenter" => new[] { "geofence_enter", "geofenceenter" },
            "geofence_exit" or "geofenceexit" => new[] { "geofence_exit", "geofenceexit" },
            "offline" or "vehicle_offline" or "device_offline" or "gps_offline" =>
                new[] { "offline", "vehicle_offline", "device_offline", "gps_offline", "deviceoffline" },
            "online" or "device_online" or "vehicle_online" or "gps_online" =>
                new[] { "online", "device_online", "vehicle_online", "gps_online", "deviceonline" },
            _ => Array.Empty<string>()
        })
        {
            set.Add(extra);
        }

        return set.ToList();
    }

    public static IReadOnlyList<string> OfflineEventTypes { get; } =
    [
        "offline", "vehicle_offline", "device_offline", "gps_offline"
    ];

    private static string MapAlarmType(string? alarm) => alarm?.ToLowerInvariant() switch
    {
        "sos" or "panic" => "sos",
        "powercut" or "poweroff" or "powerdisconnect" => "power_cut",
        "gpsantennacut" or "gpslost" or "nofix" or "gpsjamming" => "gps_lost",
        _ => "alarm"
    };

    private static string CamelToSnake(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var chars = new List<char>(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
                chars.Add('_');
            chars.Add(char.ToLowerInvariant(c));
        }
        return new string(chars.ToArray());
    }
}
