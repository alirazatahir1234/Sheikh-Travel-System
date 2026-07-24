namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

/// <summary>
/// Fleet-aware Traccar position-sync cadence (event-driven adaptive intervals).
/// </summary>
public static class TraccarAdaptiveInterval
{
    public const string ReasonMoving = "Moving";
    public const string ReasonSlowTraffic = "SlowTraffic";
    public const string ReasonIdle = "Idle";
    public const string ReasonParked = "Parked";
    public const string ReasonSos = "Sos";
    public const string ReasonDefault = "Default";

    public readonly record struct Sample(decimal SpeedKmh, bool? Ignition, string? AlarmType);

    public readonly record struct Result(int IntervalSeconds, string Reason);

    public static Result Resolve(IEnumerable<Sample> samples, TraccarOptions options)
    {
        var movingFloor = Math.Max(1, options.MovingIntervalSeconds > 0
            ? options.MovingIntervalSeconds
            : options.ResolvedPositionIntervalSeconds);
        var slow = Math.Max(movingFloor, options.SlowTrafficIntervalSeconds);
        var idle = Math.Max(slow, options.IdleIntervalSeconds);
        var parked = Math.Max(idle, options.ParkedIntervalSeconds);
        var movingThreshold = options.MovingSpeedKmh > 0 ? options.MovingSpeedKmh : 10m;

        var list = samples as IList<Sample> ?? samples.ToList();
        if (list.Count == 0)
            return new Result(parked, ReasonParked);

        var hasSos = list.Any(s => IsSos(s.AlarmType, options.SosAlarmValues));
        if (hasSos)
            return new Result(movingFloor, ReasonSos);

        if (list.Any(s => s.SpeedKmh >= movingThreshold))
            return new Result(movingFloor, ReasonMoving);

        if (list.Any(s => s.Ignition == true && s.SpeedKmh > 0.5m && s.SpeedKmh < movingThreshold))
            return new Result(slow, ReasonSlowTraffic);

        if (list.Any(s => s.Ignition == true))
            return new Result(idle, ReasonIdle);

        return new Result(parked, ReasonParked);
    }

    private static bool IsSos(string? alarm, string[] sosValues)
    {
        if (string.IsNullOrWhiteSpace(alarm)) return false;
        return sosValues.Any(v => string.Equals(v, alarm, StringComparison.OrdinalIgnoreCase));
    }
}
