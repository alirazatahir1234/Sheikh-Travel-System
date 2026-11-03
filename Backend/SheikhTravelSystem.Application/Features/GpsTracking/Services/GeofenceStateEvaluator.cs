namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Persisted per-vehicle per-zone state (<c>VehicleGeofenceState</c>).</summary>
public sealed record GeofenceZoneState(
    bool IsInside,
    DateTime SinceUtc,
    bool? PendingInside = null,
    int PendingCount = 0,
    DateTime? PendingSinceUtc = null);

public enum GeofenceTransition
{
    None,
    Enter,
    Exit
}

public sealed record GeofenceEvaluation(GeofenceZoneState State, GeofenceTransition Transition, bool StateChanged);

/// <summary>
/// Inside/outside state machine for one vehicle + zone, independent of which alert rules are on.
/// Jitter protection: a hysteresis band around the boundary plus N consecutive confirming samples.
/// </summary>
public static class GeofenceStateEvaluator
{
    /// <summary>Points closer than this to the boundary never flip the committed state.</summary>
    public const double HysteresisMeters = 25;

    /// <summary>Consecutive samples on the other side required before committing a crossing.</summary>
    public const int ConfirmSamples = 2;

    /// <summary>Same vehicle + zone + direction alert is suppressed within this window.</summary>
    public const int AlertDedupMinutes = 2;

    public static GeofenceEvaluation Evaluate(
        GeofenceZoneState? previous,
        bool rawInside,
        double boundaryDistanceMeters,
        DateTime nowUtc)
    {
        // First observation seeds state silently — no alert storm when zones/assignments are added.
        if (previous is null)
            return new GeofenceEvaluation(new GeofenceZoneState(rawInside, nowUtc), GeofenceTransition.None, true);

        var observed = rawInside;
        if (observed != previous.IsInside && boundaryDistanceMeters < HysteresisMeters)
            observed = previous.IsInside;

        if (observed == previous.IsInside)
        {
            if (previous.PendingInside is null)
                return new GeofenceEvaluation(previous, GeofenceTransition.None, false);

            return new GeofenceEvaluation(
                previous with { PendingInside = null, PendingCount = 0, PendingSinceUtc = null },
                GeofenceTransition.None,
                true);
        }

        var continuing = previous.PendingInside == observed;
        var count = continuing ? previous.PendingCount + 1 : 1;
        var pendingSince = continuing ? previous.PendingSinceUtc ?? nowUtc : nowUtc;

        if (count < ConfirmSamples)
        {
            return new GeofenceEvaluation(
                previous with { PendingInside = observed, PendingCount = count, PendingSinceUtc = pendingSince },
                GeofenceTransition.None,
                true);
        }

        return new GeofenceEvaluation(
            new GeofenceZoneState(observed, pendingSince),
            observed ? GeofenceTransition.Enter : GeofenceTransition.Exit,
            true);
    }

    /// <summary>Alert only when a rule for that direction exists.</summary>
    public static bool ShouldAlert(GeofenceTransition transition, bool alertOnEnter, bool alertOnExit)
        => transition switch
        {
            GeofenceTransition.Enter => alertOnEnter,
            GeofenceTransition.Exit => alertOnExit,
            _ => false
        };

    public static bool IsWithinDedupWindow(DateTime? lastSameAlertUtc, DateTime nowUtc)
        => lastSameAlertUtc.HasValue
           && nowUtc - lastSameAlertUtc.Value < TimeSpan.FromMinutes(AlertDedupMinutes);

    public static string EventType(GeofenceTransition transition)
        => transition == GeofenceTransition.Enter ? "geofence_enter" : "geofence_exit";
}
