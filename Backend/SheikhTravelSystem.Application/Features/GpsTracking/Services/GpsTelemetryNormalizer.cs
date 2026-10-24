namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Normalizes Traccar/Jimi (GT06) ignition for fleet status.
/// Cheap trackers often keep <c>attributes.ignition</c> stuck ON (ACC on constant
/// power, or charge bit mapped as ACC). Traccar's server-side <c>motion</c> filter
/// is then the reliable signal that the vehicle has settled to a stop → Parked.
/// </summary>
public static class GpsTelemetryNormalizer
{
    /// <summary>
    /// Effective ignition used for ingest / status.
    /// When Traccar reports <c>motion == false</c> below the moving threshold,
    /// force ignition OFF so Parked wins over false Idle.
    /// </summary>
    public static bool? ResolveIgnition(
        bool? ignition,
        bool? motion,
        decimal speedKmh,
        decimal movingThresholdKmh = GpsFleetStatusBuckets.DefaultMovingSpeedKmh)
    {
        if (motion == false && speedKmh < movingThresholdKmh)
            return false;

        return ignition;
    }
}
