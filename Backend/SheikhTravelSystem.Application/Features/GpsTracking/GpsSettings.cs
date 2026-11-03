namespace SheikhTravelSystem.Application.Features.GpsTracking;

public class GpsSettings
{
    public const string SectionName = "GpsSettings";

    public int PositionRetentionDays { get; set; } = 90;

    public int OfflineStaleMinutes { get; set; } = 30;

    /// <summary>
    /// Unused by offline detection. GpsOfflineDetectionHostedService dedups via open
    /// (IsAcknowledged = 0) vehicle_offline alerts until the vehicle comes back online.
    /// Kept for backward-compatible config binding only.
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

    /// <summary>
    /// When a Gmeet tracker stops reporting (no GPRS / stale location), SMS the device SOS number.
    /// Separate from ACC ignition SMS (Admin/Dispatcher Users.Phone).
    /// </summary>
    public GmeetOfflineSmsOptions GmeetOfflineSms { get; set; } = new();

    /// <summary>Traccar ingest filters and frozen-coordinate / teleport detection.</summary>
    public GpsPositionQualityOptions PositionQuality { get; set; } = new();

    /// <summary>GPS stop + Places proximity detection for fuel-station visit alerts.</summary>
    public FuelStationStopOptions FuelStationStop { get; set; } = new();

    /// <summary>Compare driver pump liters vs OBD fuel-level increase after leaving station.</summary>
    public FuelFraudOptions FuelFraud { get; set; } = new();
}

/// <summary>
/// Software mitigations when GPS fix is stale, LBS, or coordinates freeze while speed reports movement.
/// Does not replace physical ACC wiring or antenna repair.
/// </summary>
public class GpsPositionQualityOptions
{
    public bool RejectInvalidTraccarFixes { get; set; } = true;

    public bool RejectOutdatedTraccarFixes { get; set; } = false;

    /// <summary>When true, outdated Traccar fixes are ingested but flagged invalid.</summary>
    public bool FlagOutdatedWithoutReject { get; set; } = true;

    public bool RejectLbsOnlyFixes { get; set; } = false;

    public bool RejectZeroAccuracyFixes { get; set; } = false;

    /// <summary>Two fixes within this distance (m) count as identical coordinates.</summary>
    public double CoordinateEpsilonMeters { get; set; } = 15;

    /// <summary>Speed at or above this with unchanged coords contributes to frozen detection.</summary>
    public decimal FrozenMinSpeedKmh { get; set; } = 5m;

    /// <summary>Consecutive identical-coordinate reports before VCL lat/lng/speed stop updating.</summary>
    public int FreezeVclAfterIdenticalPoints { get; set; } = 3;

    public double TeleportMinDistanceKm { get; set; } = 3;

    public int TeleportMaxSeconds { get; set; } = 120;

    public decimal TeleportMaxPlausibleSpeedKmh { get; set; } = 180m;

    public int GpsUnreliableAlertDedupMinutes { get; set; } = 30;
}

/// <summary>
/// Detects prolonged stops near petrol-station POIs (Google Places Nearby Search).
/// Requires configured Places API; never fabricates station visits.
/// </summary>
public class FuelStationStopOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Speed at or below this value (km/h) counts as stopped.</summary>
    public decimal StopSpeedKmh { get; set; } = 3m;

    /// <summary>Minimum continuous stop duration before a Places lookup runs.</summary>
    public int MinStopMinutes { get; set; } = 4;

    /// <summary>Vehicle must be within this distance (m) of a fuel POI to raise an alert.</summary>
    public int ProximityRadiusMeters { get; set; } = 120;

    public int PlacesSearchRadiusMeters { get; set; } = 250;

    /// <summary>Suppress repeat alerts for the same vehicle within this window.</summary>
    public int DedupMinutes { get; set; } = 120;
}

/// <summary>OBD verification of driver-entered pump liters (requires FuelLevel + tank capacity).</summary>
public class FuelFraudOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Speed (km/h) indicating the vehicle left the pump island.</summary>
    public decimal LeaveSpeedKmh { get; set; } = 8m;

    /// <summary>Minimum minutes after order before verifying while still stopped.</summary>
    public int MinWaitBeforeVerifyMinutes { get; set; } = 3;

    /// <summary>Verify even without movement after this many minutes (uses latest OBD level).</summary>
    public int ForceVerifyAfterMinutes { get; set; } = 25;

    public int MaxOrderAgeMinutes { get; set; } = 180;

    /// <summary>Under-delivery beyond this volume triggers fraud alert.</summary>
    public decimal ToleranceLiters { get; set; } = 1.5m;
}

/// <summary>Gmeet offline → SOS-number SMS (site-side via Notifications:Sms).</summary>
public class GmeetOfflineSmsOptions
{
    /// <summary>Master switch. When false, offline detection still runs for ops InApp alerts.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Stale minutes before SMS. 0 = use <see cref="GpsSettings.OfflineStaleMinutes"/>.
    /// </summary>
    public int StaleMinutes { get; set; }

    /// <summary>Background scan interval for offline detection (seconds). Default 60.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;

    /// <summary>Extra name/vendor tokens treated as Gmeet (besides "gmeet" / catalog <c>gmeet_*</c>).</summary>
    public string[] ExtraNameContains { get; set; } = [];
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
