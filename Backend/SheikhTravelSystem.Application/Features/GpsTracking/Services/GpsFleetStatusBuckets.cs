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
    /// never_seen → offline → sos → parked → unknown → moving → idle → unknown.
    /// </summary>
    public static string Classify(
        bool hasGpsDevice,
        DateTime? lastUpdateUtc,
        decimal? speedKmh,
        bool? ignition,
        string? alarmType,
        DateTime utcNow,
        int offlineStaleMinutes = DefaultOfflineStaleMinutes,
        decimal movingThresholdKmh = DefaultMovingSpeedKmh,
        IReadOnlyCollection<string>? sosAlarmValues = null)
    {
        var sos = sosAlarmValues is { Count: > 0 }
            ? sosAlarmValues
            : (IReadOnlyCollection<string>)["sos", "panic"];

        if (lastUpdateUtc is null)
            return hasGpsDevice ? "never_seen" : "offline";

        var ageMinutes = (utcNow - lastUpdateUtc.Value).TotalMinutes;
        if (ageMinutes > offlineStaleMinutes)
            return "offline";

        if (!string.IsNullOrWhiteSpace(alarmType) &&
            sos.Any(v => string.Equals(v, alarmType.Trim(), StringComparison.OrdinalIgnoreCase)))
            return "sos";

        var speed = speedKmh ?? 0m;

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

        // Ignition null / unwired with low speed — insufficient telemetry.
        return "unknown";
    }
}
