namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Timezone maths for tenant-local days. Timestamps are stored in UTC; these helpers find the UTC
/// instants that bound a tenant's local calendar day (e.g. "today" in Asia/Karachi starts 19:00 UTC
/// the previous day, not at UTC midnight).
/// </summary>
public static class TenantTimeZoneHelper
{
    /// <summary>
    /// Resolves an IANA (or Windows) timezone id. Blank or unknown ids fall back to
    /// <paramref name="defaultTimeZoneId"/>, then UTC.
    /// </summary>
    public static TimeZoneInfo Resolve(string? timeZoneId, string defaultTimeZoneId)
    {
        if (TryFind(timeZoneId, out var zone))
            return zone;
        return TryFind(defaultTimeZoneId, out var fallback) ? fallback : TimeZoneInfo.Utc;
    }

    /// <summary>True when <paramref name="timeZoneId"/> is a timezone this server can resolve.</summary>
    public static bool IsValid(string? timeZoneId) => TryFind(timeZoneId, out _);

    public static DateOnly UtcToLocalDate(DateTime utc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));

    /// <summary>UTC instant of local midnight at the start of <paramref name="localDate"/>.</summary>
    public static DateTime LocalDayStartUtc(DateOnly localDate, TimeZoneInfo timeZone) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue), timeZone),
            DateTimeKind.Utc);

    /// <summary>UTC instant of the start of the tenant's local "today".</summary>
    public static DateTime LocalTodayStartUtc(TimeZoneInfo timeZone, DateTime utcNow) =>
        LocalDayStartUtc(UtcToLocalDate(utcNow, timeZone), timeZone);

    /// <summary>[start, endExclusive) in UTC for the local calendar days <paramref name="fromLocal"/>..<paramref name="toLocal"/>.</summary>
    public static (DateTime StartUtc, DateTime EndExclusiveUtc) LocalDayRangeUtc(
        DateOnly fromLocal, DateOnly toLocal, TimeZoneInfo timeZone) =>
        (LocalDayStartUtc(fromLocal, timeZone), LocalDayStartUtc(toLocal.AddDays(1), timeZone));

    /// <summary>
    /// UTC bounds (inclusive end) for a date preset in the given timezone: today, yesterday, last7, last30
    /// are local calendar days; last24 is a rolling window. Null for unknown presets.
    /// </summary>
    public static (DateTime From, DateTime To)? ResolvePresetRangeUtc(string? preset, TimeZoneInfo timeZone, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return null;

        var localToday = UtcToLocalDate(utcNow, timeZone);
        DateTime Start(int dayOffset) => LocalDayStartUtc(localToday.AddDays(dayOffset), timeZone);

        return preset.Trim().ToLowerInvariant() switch
        {
            "today" => (Start(0), Start(1).AddTicks(-1)),
            "yesterday" => (Start(-1), Start(0).AddTicks(-1)),
            "last7" => (Start(-6), Start(1).AddTicks(-1)),
            "last30" => (Start(-29), Start(1).AddTicks(-1)),
            "last24" => (DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).AddHours(-24), DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)),
            _ => null
        };
    }

    private static bool TryFind(string? timeZoneId, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return false;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
