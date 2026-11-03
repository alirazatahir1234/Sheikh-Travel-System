using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TraccarPositionIngestMapper
{
    public static PositionIngestSourceContext ToSourceContext(
        TraccarPosition position,
        decimal? movingThresholdKmh = null)
    {
        var sat = position.Attributes.Satellites ?? position.Attributes.Sat;
        var lbs = GpsPositionQualityEvaluator.IsLbsOnlyFromTraccar(
            position.Valid, sat, position.Accuracy);
        var speedKmh = (decimal)(position.Speed * 1.852);
        var threshold = movingThresholdKmh is > 0
            ? movingThresholdKmh.Value
            : GpsFleetStatusBuckets.DefaultMovingSpeedKmh;
        var clearSticky = GpsTelemetryNormalizer.ShouldClearStickyIgnition(
            position.Attributes.Ignition,
            position.Attributes.Motion,
            speedKmh,
            threshold);

        return new PositionIngestSourceContext(
            TraccarValid: position.Valid,
            TraccarOutdated: position.Outdated,
            TraccarAccuracyMeters: position.Accuracy,
            FixTimeUtc: position.FixTime.ToUniversalTime(),
            SatelliteCount: sat,
            LbsOnlyFix: lbs,
            ClearStickyIgnition: clearSticky);
    }
}
