namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Pure status bucket for fleet KPIs — keep in sync with frontend
/// <c>resolveFleetStatus</c> and SQL in GpsFleetStatusCalculator.
/// </summary>
public static class GpsFleetStatusBuckets
{
    public const int DefaultOfflineStaleMinutes = 30;
    public const decimal DefaultMovingSpeedKmh = 10m;

    /// <summary>
    /// Connectivity first, then operational when online:
    /// no tracker → offline; never_seen → offline → sos → parked → unknown → moving → idle → parked.
    /// Online buckets require an assigned live GPS device — orphan VehicleCurrentLocation
    /// rows must not inflate Online KPIs.
    /// </summary>
    /// <param name="motion">
    /// Optional Traccar <c>attributes.motion</c>. When false at low speed, classify as
    /// Parked for fleet KPIs even if ACC/ignition is ON (idle engine, key on).
    /// Does not change stored/display ignition — only the status bucket.
    /// </param>
    public static string Classify(
        bool hasGpsDevice,
        DateTime? lastUpdateUtc,
        decimal? speedKmh,
        bool? ignition,
        string? alarmType,
        DateTime utcNow,
        int offlineStaleMinutes = DefaultOfflineStaleMinutes,
        decimal movingThresholdKmh = DefaultMovingSpeedKmh,
        IReadOnlyCollection<string>? sosAlarmValues = null,
        bool? motion = null)
    {
        var sos = sosAlarmValues is { Count: > 0 }
            ? sosAlarmValues
            : (IReadOnlyCollection<string>)["sos", "panic"];

        // No tracker installed (or device row missing/deleted) — never Online,
        // even when leftover VCL coordinates exist.
        if (!hasGpsDevice)
            return "offline";

        if (lastUpdateUtc is null)
            return "never_seen";

        var ageMinutes = (utcNow - lastUpdateUtc.Value).TotalMinutes;
        if (ageMinutes > offlineStaleMinutes)
            return "offline";

        if (!string.IsNullOrWhiteSpace(alarmType) &&
            sos.Any(v => string.Equals(v, alarmType.Trim(), StringComparison.OrdinalIgnoreCase)))
            return "sos";

        var speed = speedKmh ?? 0m;

        // Settled (no motion) at low speed → Parked for KPIs; ACC may still be ON in UI.
        if (motion == false && speed < movingThresholdKmh)
            return "parked";

        // Ignition OFF + below moving threshold → parked (speed 0 and low GPS drift).
        if (ignition == false && speed < movingThresholdKmh)
            return "parked";

        // Ignition OFF + high speed → contradictory.
        if (ignition == false && speed >= movingThresholdKmh)
            return "unknown";

        if (speed >= movingThresholdKmh)
            return "moving";

        if (ignition == true)
            return "idle";

        // Ignition null / unwired with low speed — treat as parked (stationary), not Idle.
        // Jimi units without a working ACC wire otherwise stuck on Idle forever.
        return "parked";
    }
}
