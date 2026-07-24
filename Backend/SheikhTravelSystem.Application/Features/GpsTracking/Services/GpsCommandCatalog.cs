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
        new("positionSingle", "Request Position", "positionSingle", GpsPermissions.CommandPositionRequest, null,                    false, false),
        new("restart",        "Restart Device",   "rebootDevice",   GpsPermissions.CommandRestart,          null,                    false, false),
        new("relayOn",        "Relay ON",         "outputControl",  GpsPermissions.CommandRelay,            "SupportsRelay",         true,  false),
        new("relayOff",       "Relay OFF",        "outputControl",  GpsPermissions.CommandRelay,            "SupportsRelay",         true,  false),
        new("buzzer",         "Buzzer",           "custom",         GpsPermissions.CommandBuzzer,           null,                    false, false),
        new("customSms",      "Custom SMS",       null,             GpsPermissions.CommandCustomSms,        null,                    false, false),
        new("custom",         "Custom Command",   "custom",         GpsPermissions.CommandSend,             null,                    false, false),
        new("status",         "Device Status",    null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("version",        "Firmware Version", null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("iccid",          "SIM ICCID",        null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("imsi",           "SIM IMSI",         null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("param",          "Basic Parameters", null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("battery",        "Battery Info",     null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("signal",         "Network / Signal", null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("apn",            "Set APN",          null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("server",         "Server Settings",  null,             GpsPermissions.CommandSend,             null,                    false, false),
        new("heartbeat",      "Heartbeat Interval", null,           GpsPermissions.CommandSend,             null,                    false, false),
        new("timezone",       "Timezone",         null,             GpsPermissions.CommandSend,             null,                    false, false),
    ];

    public static Definition? Find(string type) =>
        All.FirstOrDefault(d => string.Equals(d.Type, type, StringComparison.OrdinalIgnoreCase));
}
