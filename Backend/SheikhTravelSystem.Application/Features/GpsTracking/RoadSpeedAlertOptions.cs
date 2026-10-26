namespace SheikhTravelSystem.Application.Features.GpsTracking;

/// <summary>
/// Tunables for road-posted speed-limit overspeed alerts (module <c>ROAD_SPEED_ALERTS</c>).
/// Google Maps credential remains under <see cref="GoogleMapsOptions"/> — never commit real keys.
/// </summary>
public sealed class RoadSpeedAlertOptions
{
    public const string SectionName = "RoadSpeedAlerts";

    /// <summary>
    /// Global kill switch for Roads Speed Limits API calls (independent of tenant module toggle).
    /// When false, the evaluator never calls Google and never raises road-speed alerts.
    /// </summary>
    public bool ApiEnabled { get; set; } = true;

    /// <summary>Alert when currentSpeed ≥ roadLimitKmh + this margin. Default 20 km/h.</summary>
    public decimal OverspeedMarginKmh { get; set; } = 20m;

    /// <summary>
    /// Skip Roads API / re-eval when vehicle has moved less than this many metres since the last
    /// successful lookup for that vehicle (same segment reuse). Default ~75 m.
    /// </summary>
    public double MinMovementMeters { get; set; } = 75;

    /// <summary>
    /// Skip evaluation entirely when current speed is below this (no realistic roadLimit+margin breach).
    /// Default 30 km/h (covers 10 km/h posted + 20 margin).
    /// </summary>
    public decimal MinSpeedToEvaluateKmh { get; set; } = 30m;

    /// <summary>Durable + memory cache TTL for speed-limit lookups. Default 24 h.</summary>
    public int CacheTtlHours { get; set; } = 24;

    /// <summary>
    /// Approximate grid cell size in degrees for cache keys when PlaceId is unknown
    /// (~0.0009° ≈ 100 m at mid-latitudes).
    /// </summary>
    public double GridCellDegrees { get; set; } = 0.0009;

    /// <summary>Re-alert at most once per this many minutes while continuously overspeed. Default 5.</summary>
    public int AlertDedupMinutes { get; set; } = 5;

    /// <summary>Consecutive Google API failures before opening the circuit.</summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>How long to skip Google calls after the circuit opens.</summary>
    public int CircuitBreakerCooldownMinutes { get; set; } = 15;
}
