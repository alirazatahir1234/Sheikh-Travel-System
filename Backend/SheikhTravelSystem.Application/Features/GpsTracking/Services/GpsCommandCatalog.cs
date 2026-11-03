using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Fallback catalog for legacy Traccar command types and tenant permission mapping.
/// Prefer GpsCommandDefinitions + templates via the Control Center translator.
/// </summary>
public static class GpsCommandCatalog
{
    public sealed record Definition(
        string Type,
        string Label,
        string? TraccarType,
        string Permission,
        string? CapabilityColumn,
        bool RequiresEngineSafetyCheck,
        bool NotifyAllUsers);

    public static readonly Definition[] All =
    [
        new("engineStop",     "Engine Stop",      "engineStop",     GpsPermissions.CommandEngineCutoff,    "SupportsEngineCutoff", true,  true),
        new("engineResume",   "Engine Resume",    "engineResume",   GpsPermissions.CommandEngineCutoff,    "SupportsEngineCutoff", false, true),
        new("positionSingle", "URL / Location",   "positionSingle", GpsPermissions.CommandPositionRequest, null,                    false, false),
        new("restart",        "Restart Device",   "rebootDevice",   GpsPermissions.CommandRestart,          null,                    false, false),
        new("relayOn",        "Relay ON",         "outputControl",  GpsPermissions.CommandRelay,            "SupportsRelay",         true,  false),
        new("relayOff",       "Relay OFF",        "outputControl",  GpsPermissions.CommandRelay,            "SupportsRelay",         true,  false),
        new("buzzer",         "Buzzer",           "custom",         GpsPermissions.CommandBuzzer,           null,                    false, false),
        new("customSms",      "Custom SMS",       null,             GpsPermissions.CommandCustomSms,        null,                    false, false),
        new("custom",         "Custom Command",   "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("status",         "Device Status",    "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("check",          "Device Check",     "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("version",        "Firmware Version", "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("iccid",          "SIM ICCID",        "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("imsi",           "SIM IMSI",         "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("param",          "Basic Parameters", "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("battery",        "Battery Info",     "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("signal",         "Network / Signal", "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("gprsset",        "GPRSSET",          "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("apn",            "Query APN",        "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("server",         "Network Settings", "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("heartbeat",      "Heartbeat Interval", null,           GpsPermissions.CommandSend,             null,                    false, false),
        new("timezone",       "Timezone",         null,             GpsPermissions.CommandSend,             null,                    false, false),
    ];

    public static Definition? Find(string type) =>
        All.FirstOrDefault(d => string.Equals(d.Type, type, StringComparison.OrdinalIgnoreCase));
}
