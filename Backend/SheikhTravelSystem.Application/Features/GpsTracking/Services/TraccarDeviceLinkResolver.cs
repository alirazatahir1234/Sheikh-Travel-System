namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Resolves SheikhGo GpsDevices → Traccar device id without crashing when duplicates exist.
/// Match key is TraccarDeviceId (assigned via IMEI/UniqueId sync) — never phone.
/// </summary>
public static class TraccarDeviceLinkResolver
{
    public readonly record struct DeviceLink(
        int TraccarDeviceId,
        int GpsDeviceId,
        int? VehicleId,
        DateTime? LastSeenAt);

    /// <summary>
    /// One canonical GpsDevice per TraccarDeviceId.
    /// Preference: vehicle-linked → freshest LastSeenAt → lowest GpsDeviceId.
    /// </summary>
    public static Dictionary<int, DeviceLink> PreferCanonical(IEnumerable<DeviceLink> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .GroupBy(r => r.TraccarDeviceId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(r => r.VehicleId.HasValue ? 0 : 1)
                    .ThenByDescending(r => r.LastSeenAt ?? DateTime.MinValue)
                    .ThenBy(r => r.GpsDeviceId)
                    .First());
    }
}
