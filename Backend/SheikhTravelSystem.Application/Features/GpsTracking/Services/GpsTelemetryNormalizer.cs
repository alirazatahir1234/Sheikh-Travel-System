namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Normalizes Traccar/Jimi (GT06/VG03) ignition for ingest and UI.
/// <list type="number">
/// <item>When <c>attributes.ignition</c> (ACC) is present, use it — never override with motion.</item>
/// <item>
/// When ACC is omitted (common on command-result / heartbeat packets) and Traccar reports
/// <c>motion == false</c> at low speed, treat as Ignition OFF so a prior ACC=ON value cannot
/// stick forever via COALESCE and show false ON while the vehicle is off.
/// </item>
/// </list>
/// Fleet Parked vs Idle still uses motion separately in <see cref="GpsFleetStatusBuckets"/>.
/// </summary>
public static class GpsTelemetryNormalizer
{
    /// <summary>
    /// Effective ignition for ingest / Commands strip / VCL.
    /// </summary>
    public static bool? ResolveIgnition(
        bool? ignition,
        bool? motion,
        decimal speedKmh,
        decimal movingThresholdKmh = GpsFleetStatusBuckets.DefaultMovingSpeedKmh)
    {
        // 1) Trust device ACC when the packet includes it.
        if (ignition.HasValue)
            return ignition;

        // 2) Sparse GT06 packets omit ACC — settled + no motion ⇒ OFF (not "keep stale ON").
        if (motion == false && speedKmh < movingThresholdKmh)
            return false;

        return null;
    }
}
