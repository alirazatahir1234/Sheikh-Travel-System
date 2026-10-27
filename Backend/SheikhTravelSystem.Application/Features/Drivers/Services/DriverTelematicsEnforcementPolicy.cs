namespace SheikhTravelSystem.Application.Features.Drivers.Services;

/// <summary>
/// Ops-safe telematics enforcement thresholds (product-signed).
/// <list type="bullet">
/// <item>Each harsh (or overspeed) GpsAlertEvent with a DriverId creates one DriverViolation (deduped by GpsAlertId).</item>
/// <item>Status escalation uses harsh violations only: ≥ <see cref="HarshViolationSuspendThreshold"/> in
/// <see cref="RollingWindowDays"/> days → <c>DriverStatus.Suspended</c>.</item>
/// <item>Suspended blocks new assignments via DriverAssignmentGuard; ops can reverse status manually.</item>
/// <item>No NeedsReview enum exists — Suspended is the reversible holding state.</item>
/// </list>
/// </summary>
public static class DriverTelematicsEnforcementPolicy
{
    public const int HarshViolationSuspendThreshold = 3;
    public const int RollingWindowDays = 7;
    public const string CreatedBySystem = "system:telematics";

    public const string ViolationTypeHarshBrake = "HarshBraking";
    public const string ViolationTypeHarshAccel = "HarshAcceleration";
    public const string ViolationTypeOverspeed = "Overspeed";

    public static bool ShouldCreateViolation(string eventType) =>
        eventType.Equals("harsh_braking", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("harsh_acceleration", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("speed_exceeded", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("overspeed", StringComparison.OrdinalIgnoreCase) ||
        eventType.Equals("road_speed_exceeded", StringComparison.OrdinalIgnoreCase);

    public static bool CountsTowardSuspend(string violationType) =>
        violationType.Equals(ViolationTypeHarshBrake, StringComparison.OrdinalIgnoreCase) ||
        violationType.Equals(ViolationTypeHarshAccel, StringComparison.OrdinalIgnoreCase);

    public static string ViolationTypeForEvent(string eventType) => eventType.ToLowerInvariant() switch
    {
        "harsh_braking" => ViolationTypeHarshBrake,
        "harsh_acceleration" => ViolationTypeHarshAccel,
        "speed_exceeded" or "overspeed" or "road_speed_exceeded" => ViolationTypeOverspeed,
        _ => eventType
    };

    public static string SeverityForEvent(string eventType) => eventType.ToLowerInvariant() switch
    {
        "harsh_braking" or "harsh_acceleration" => "High",
        "speed_exceeded" or "overspeed" or "road_speed_exceeded" => "Medium",
        _ => "Medium"
    };
}
