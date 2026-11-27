namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Alert event types raised by geofence rules (beyond geofence_enter / geofence_exit).</summary>
public static class GeofenceRuleEvents
{
    public const string Dwell = "geofence_dwell";
    public const string Overspeed = "geofence_overspeed";
    public const string Idle = "geofence_idle";
    public const string NotArrived = "geofence_not_arrived";
    public const string AfterHoursExit = "geofence_after_hours_exit";

    public static readonly IReadOnlyList<string> All = [Dwell, Overspeed, Idle, NotArrived, AfterHoursExit];
}

/// <summary>A geofence's rules as stored on the <c>Geofences</c> row. Null thresholds mean the rule is off.</summary>
public sealed record GeofenceRules(
    int? DwellMinutes,
    decimal? SpeedLimitKmh,
    int? IdleMinutes,
    TimeSpan? NotArrivedBy,
    bool EngineBlockOnExit,
    GeofenceSchedule Schedule,
    bool AfterHoursExitCritical,
    int CooldownMinutes)
{
    public static readonly GeofenceRules None = new(null, null, null, null, false, GeofenceSchedule.Always, false, 15);
}

/// <summary>
/// Pure rule checks. Rules only fire while the schedule is active (always, when no schedule is set). The one
/// exception is <see cref="IsAfterHoursExit"/>: an exit outside the schedule raises a critical alert when the
/// geofence asks for it. Dwell and idle fire once per visit; speed repeats after the cooldown.
/// </summary>
public static class GeofenceRuleEvaluator
{
    public static bool RulesActive(GeofenceRules rules, DateTime nowLocal) => rules.Schedule.IsActiveAt(nowLocal);

    public static bool IsSpeedBreach(GeofenceRules rules, decimal speedKmh)
        => rules.SpeedLimitKmh is { } limit && limit > 0 && speedKmh > limit;

    public static bool IsIdleBreach(GeofenceRules rules, GeofenceVisitState visit, DateTime nowUtc)
        => rules.IdleMinutes is { } minutes && minutes > 0
           && !visit.IdleBreached
           && visit.IdleSinceUtc is { } since
           && nowUtc - since >= TimeSpan.FromMinutes(minutes);

    public static bool IsDwellBreach(GeofenceRules rules, GeofenceVisitState visit, DateTime nowUtc)
        => rules.DwellMinutes is { } minutes && minutes > 0
           && !visit.DwellBreached
           && nowUtc - visit.EnteredUtc >= TimeSpan.FromMinutes(minutes);

    /// <summary>The exit happened outside a set schedule (never true for "always").</summary>
    public static bool IsAfterHoursExit(GeofenceRules rules, DateTime exitLocal)
        => !rules.Schedule.IsAlways && !rules.Schedule.IsActiveAt(exitLocal);

    /// <summary>"No assigned vehicle arrived by HH:MM": past the deadline on an active day, with no arrival today.</summary>
    public static bool IsNotArrivedDue(GeofenceRules rules, DateTime nowLocal, bool anyArrivalToday)
        => rules.NotArrivedBy is { } deadline
           && !anyArrivalToday
           && nowLocal.TimeOfDay >= deadline
           && rules.Schedule.IsDayActive(nowLocal.DayOfWeek);

    public static bool IsInCooldown(DateTime? lastAlertUtc, DateTime nowUtc, int cooldownMinutes)
        => lastAlertUtc is { } last && cooldownMinutes > 0 && nowUtc - last < TimeSpan.FromMinutes(cooldownMinutes);
}
