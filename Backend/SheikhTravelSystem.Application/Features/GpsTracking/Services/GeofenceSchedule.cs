namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// When a geofence's rules are active, in the company's local time. <see cref="Days"/> null means always.
/// Days is a bitmask: Mon 1, Tue 2, Wed 4, Thu 8, Fri 16, Sat 32, Sun 64. Missing hours, or start == end, mean the
/// whole day. When end is earlier than start the window runs past midnight (20:00–06:00): the early-morning part
/// belongs to the day the window started.
/// </summary>
public sealed record GeofenceSchedule(byte? Days, TimeSpan? Start, TimeSpan? End)
{
    public const byte AllDays = 127;

    public static readonly GeofenceSchedule Always = new(null, null, null);

    public bool IsAlways => Days is null;

    public static byte DayBit(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => 1,
        DayOfWeek.Tuesday => 2,
        DayOfWeek.Wednesday => 4,
        DayOfWeek.Thursday => 8,
        DayOfWeek.Friday => 16,
        DayOfWeek.Saturday => 32,
        _ => 64
    };

    public bool IsDayActive(DayOfWeek day) => Days is not { } days || (days & DayBit(day)) != 0;

    public bool IsActiveAt(DateTime local)
    {
        if (Days is null)
            return true;

        var time = local.TimeOfDay;
        if (Start is not { } start || End is not { } end || start == end)
            return IsDayActive(local.DayOfWeek);

        if (start < end)
            return IsDayActive(local.DayOfWeek) && time >= start && time < end;

        // Overnight window.
        return (time >= start && IsDayActive(local.DayOfWeek))
               || (time < end && IsDayActive(local.AddDays(-1).DayOfWeek));
    }
}
