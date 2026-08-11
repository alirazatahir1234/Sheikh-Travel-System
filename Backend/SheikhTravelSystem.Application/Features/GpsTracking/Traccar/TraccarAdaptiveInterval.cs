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
        // When ignition is unknown, treat modest motion as Moving so we never fall into the
        // Parked (minutes) bucket for creeping/unknown-ignition vehicles.
        var unknownIgnitionMotion = options.UnknownIgnitionMovingSpeedKmh > 0
            ? options.UnknownIgnitionMovingSpeedKmh
            : 2m;

        var list = samples as IList<Sample> ?? samples.ToList();
        if (list.Count == 0)
            return new Result(parked, ReasonParked);

        var hasSos = list.Any(s => IsSos(s.AlarmType, options.SosAlarmValues));
        if (hasSos)
            return new Result(movingFloor, ReasonSos);

        if (list.Any(s => s.SpeedKmh >= movingThreshold))
            return new Result(movingFloor, ReasonMoving);

        // Known ignition ON + crawl → slow traffic (preserves 15s band when ignition is reported).
        if (list.Any(s => s.Ignition == true && s.SpeedKmh > 0.5m && s.SpeedKmh < movingThreshold))
            return new Result(slow, ReasonSlowTraffic);

        // Ignition missing but vehicle is clearly moving → treat as Moving (not Parked).
        if (list.Any(s => s.Ignition == null && s.SpeedKmh > unknownIgnitionMotion))
            return new Result(movingFloor, ReasonMoving);

        // Idle: ignition ON at rest, OR ignition unknown at rest (VG03 often omits ignition).
        if (list.Any(s => s.Ignition == true || s.Ignition == null))
            return new Result(idle, ReasonIdle);

        // Parked only when every sample has ignition explicitly OFF (and no motion/SOS above).
        return new Result(parked, ReasonParked);
    }

    private static bool IsSos(string? alarm, string[] sosValues)
    {
        if (string.IsNullOrWhiteSpace(alarm)) return false;
        return sosValues.Any(v => string.Equals(v, alarm, StringComparison.OrdinalIgnoreCase));
    }
}
