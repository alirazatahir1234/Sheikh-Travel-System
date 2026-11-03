namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Normalizes Traccar/Jimi (GT06/VG03) ignition for ingest and UI.
/// <list type="number">
/// <item>When <c>attributes.ignition</c> (ACC) is present, use it — never override with motion.</item>
/// <item>
/// When ACC is omitted, return null (Unknown) — do not invent hardware ACC / OFF.
/// Use <see cref="ShouldClearStickyIgnition"/> so ingest can clear a prior sticky ON
/// without claiming Ignition OFF.
/// </item>
/// </list>
/// Fleet Parked vs Idle still uses motion separately in <see cref="GpsFleetStatusBuckets"/>.
/// </summary>
public static class GpsTelemetryNormalizer
{
    /// <summary>
    /// Effective ignition for ingest / Commands strip / VCL.
    /// Null means ACC not reported — UI must show Unavailable / Unknown, not OFF.
    /// </summary>
    public static bool? ResolveIgnition(
        bool? ignition,
        bool? motion,
        decimal speedKmh,
        decimal movingThresholdKmh = GpsFleetStatusBuckets.DefaultMovingSpeedKmh)
    {
        // Trust device ACC when the packet includes it. Never invent OFF from motion.
        // motion/speed retained for call-site compatibility; sticky-ON clear uses ShouldClearStickyIgnition.
#pragma warning disable IDE0060
        _ = motion;
        _ = speedKmh;
        _ = movingThresholdKmh;
#pragma warning restore IDE0060
        return ignition;
    }

    /// <summary>
    /// Sparse GT06 packets omit ACC. When Traccar reports settled (motion=false) at low speed,
    /// clear a prior sticky ON via COALESCE bypass — store null, not false.
    /// </summary>
    public static bool ShouldClearStickyIgnition(
        bool? ignition,
        bool? motion,
        decimal speedKmh,
        decimal movingThresholdKmh = GpsFleetStatusBuckets.DefaultMovingSpeedKmh)
    {
        if (ignition.HasValue)
            return false;

        return motion == false && speedKmh < movingThresholdKmh;
    }
}
