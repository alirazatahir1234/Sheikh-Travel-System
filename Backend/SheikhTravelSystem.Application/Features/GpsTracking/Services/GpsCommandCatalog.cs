using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Single source of truth for supported GPS device command types — what permission it requires,
/// which device capability column gates it, whether it needs the engine-safety precondition, and
/// what Traccar command type it dispatches as. Replaces the old hardcoded 4-value allowlist in
/// SendDeviceCommandCommandValidator.
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

    // TraccarType values for "restart"/"relayOn"/"relayOff" ("rebootDevice"/"outputControl") are
    // best-recollection of Traccar's command API, not verified against this deployment — confirm
    // via GetSupportedCommandTypesAsync against a live linked device before relying on them.
    // "buzzer" has no universal Traccar command type; it rides on "custom" as a per-model gap.
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
    ];

    public static Definition? Find(string type) =>
        All.FirstOrDefault(d => string.Equals(d.Type, type, StringComparison.OrdinalIgnoreCase));
}
