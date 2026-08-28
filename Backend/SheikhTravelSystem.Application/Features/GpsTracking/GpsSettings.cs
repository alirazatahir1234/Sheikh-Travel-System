namespace SheikhTravelSystem.Application.Features.GpsTracking;

public class GpsSettings
{
    public const string SectionName = "GpsSettings";

    public int PositionRetentionDays { get; set; } = 90;

    public int OfflineStaleMinutes { get; set; } = 10;
    
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
}
