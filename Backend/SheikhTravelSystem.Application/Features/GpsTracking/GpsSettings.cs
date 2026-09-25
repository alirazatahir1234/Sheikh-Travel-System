namespace SheikhTravelSystem.Application.Features.GpsTracking;

public class GpsSettings
{
    public const string SectionName = "GpsSettings";

    public int PositionRetentionDays { get; set; } = 90;

    public int OfflineStaleMinutes { get; set; } = 30;

    /// <summary>
    /// Minimum cooldown before raising another vehicle_offline event for the same vehicle.
    /// </summary>
    public int OfflineAlertCooldownMinutes { get; set; } = 120;

    /// <summary>
    /// Fleet-status moving threshold in km/h (for dashboard classification).
    /// </summary>
    public decimal FleetMovingSpeedKmh { get; set; } = 10m;

    public decimal LowBatteryThresholdPercent { get; set; } = 20m;

    public int CommandRetryIntervalSeconds { get; set; } = 60;

    public int CommandAckTimeoutMinutes { get; set; } = 5;

    public int CommandMaxRetries { get; set; } = 3;

    /// <summary>Must exceed a year so Trends/Analytics can show 12 months of fleet-status history.</summary>
    public int FleetStatusSnapshotRetentionDays { get; set; } = 400;

    /// <summary>
    /// Explainable fleet-health scoring (Live Map). Weights and bands are configurable —
    /// see <see cref="FleetHealth"/>. Null factors are Unknown (excluded), never treated as healthy.
    /// </summary>
    public GpsFleetHealthOptions FleetHealth { get; set; } = new();
}

/// <summary>
/// Transparent health scoring. Documented factor weights — do not invent scores for missing data.
/// Operational status (Parked/Idle/Moving) is never a health factor.
/// </summary>
public class GpsFleetHealthOptions
{
    public GpsFleetHealthWeights Weights { get; set; } = new();
    public GpsFleetHealthBands Bands { get; set; } = new();
    public GpsFleetHealthFactorScores FactorScores { get; set; } = new();

    /// <summary>Battery % below this → Critical.</summary>
    public decimal CriticalBatteryPercent { get; set; } = 10m;

    /// <summary>Battery % below this (but ≥ Critical) → Attention.</summary>
    public decimal AttentionBatteryPercent { get; set; } = 25m;

    /// <summary>GSM/RSSI below this → Attention (aligned with operator WeakGsmThreshold).</summary>
    public int AttentionGsmSignal { get; set; } = 12;
}

public class GpsFleetHealthWeights
{
    public int GpsFreshness { get; set; } = 20;
    public int TrackerBattery { get; set; } = 15;
    public int GsmSignal { get; set; } = 10;
    public int CriticalAlerts { get; set; } = 25;
    public int Maintenance { get; set; } = 20;
    public int Insurance { get; set; } = 10;
}

public class GpsFleetHealthBands
{
    public int OptimalMin { get; set; } = 90;
    public int HealthyMin { get; set; } = 70;
    public int AttentionMin { get; set; } = 40;
}

public class GpsFleetHealthFactorScores
{
    public int Optimal { get; set; } = 100;
    public int Attention { get; set; } = 50;
    public int Critical { get; set; } = 0;
}
