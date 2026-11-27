namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>One open visit (a vehicle inside one geofence), persisted in <c>GeofenceVisits</c>.</summary>
public sealed record GeofenceVisitState(
    DateTime EnteredUtc,
    DateTime LastPositionUtc,
    int IdleSeconds,
    DateTime? IdleSinceUtc,
    decimal MaxSpeedKmh,
    bool DwellBreached = false,
    bool SpeedBreached = false,
    bool IdleBreached = false);

/// <summary>
/// Visit bookkeeping from the positions a vehicle reports while inside a geofence.
/// <list type="bullet">
/// <item>Idle = ignition on and speed under <see cref="IdleSpeedKmh"/>. Unknown ignition is not idle.</item>
/// <item>Idle time accrues for an interval whose starting fix was idle; a gap longer than
/// <see cref="MaxCountedGap"/> is not counted and restarts the idle streak.</item>
/// <item>Fixes at or before the last one change nothing, so replays never count time twice.</item>
/// </list>
/// </summary>
public static class GeofenceVisitTracker
{
    public const decimal IdleSpeedKmh = 3m;

    public static readonly TimeSpan MaxCountedGap = TimeSpan.FromMinutes(10);

    public static bool IsIdle(decimal speedKmh, bool? ignition) => ignition == true && speedKmh < IdleSpeedKmh;

    public static GeofenceVisitState Open(DateTime enteredUtc, DateTime nowUtc, decimal speedKmh, bool? ignition)
        => new(
            enteredUtc,
            nowUtc,
            0,
            IsIdle(speedKmh, ignition) ? nowUtc : null,
            Math.Max(0, speedKmh));

    public static GeofenceVisitState Observe(GeofenceVisitState visit, DateTime nowUtc, decimal speedKmh, bool? ignition)
    {
        if (nowUtc <= visit.LastPositionUtc)
            return visit;

        var gap = nowUtc - visit.LastPositionUtc;
        var idleNow = IsIdle(speedKmh, ignition);
        var idleSeconds = visit.IdleSeconds;
        DateTime? idleSince;

        if (gap > MaxCountedGap)
        {
            idleSince = idleNow ? nowUtc : null;
        }
        else
        {
            if (visit.IdleSinceUtc is not null)
                idleSeconds += (int)gap.TotalSeconds;
            idleSince = idleNow ? visit.IdleSinceUtc ?? nowUtc : null;
        }

        return visit with
        {
            LastPositionUtc = nowUtc,
            IdleSeconds = idleSeconds,
            IdleSinceUtc = idleSince,
            MaxSpeedKmh = Math.Max(visit.MaxSpeedKmh, speedKmh)
        };
    }
}
