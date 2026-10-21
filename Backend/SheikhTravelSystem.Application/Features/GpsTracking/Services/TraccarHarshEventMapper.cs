namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Maps Traccar event/alarm spellings onto canonical GpsAlertEvents.EventType values
/// (<c>harsh_braking</c> / <c>harsh_acceleration</c>) used by rollup, ranking, and enforcement.
/// </summary>
public static class TraccarHarshEventMapper
{
    public const string HarshBraking = "harsh_braking";
    public const string HarshAcceleration = "harsh_acceleration";

    public static bool IsCanonicalHarsh(string? eventType) =>
        string.Equals(eventType, HarshBraking, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(eventType, HarshAcceleration, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns canonical harsh type when the Traccar type or alarm attribute is a harsh event; otherwise null.
    /// </summary>
    public static string? TryMapCanonical(string? traccarType, string? alarmAttribute = null)
    {
        var fromType = Classify(traccarType);
        if (fromType is not null)
            return fromType;

        if (string.Equals(traccarType, "alarm", StringComparison.OrdinalIgnoreCase))
            return Classify(alarmAttribute);

        return Classify(alarmAttribute);
    }

    public static bool LooksLikeHarsh(string? value) => Classify(value) is not null;

    private static string? Classify(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (s.Contains("brak", StringComparison.OrdinalIgnoreCase))
            return HarshBraking;
        if (s.Contains("accel", StringComparison.OrdinalIgnoreCase))
            return HarshAcceleration;
        return null;
    }
}
