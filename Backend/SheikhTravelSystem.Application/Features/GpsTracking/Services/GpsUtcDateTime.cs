namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// SQL DATETIME2 / Dapper values arrive as <see cref="DateTimeKind.Unspecified"/>.
/// Marking them UTC ensures System.Text.Json emits a trailing Z so browsers do not
/// treat the instant as local time (e.g. PKT +5h → false Offline on Live Map).
/// </summary>
public static class GpsUtcDateTime
{
    public static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? AsUtc(value.Value) : null;

    /// <summary>
    /// A report window cannot end in the future: hours that have not happened yet
    /// must not be counted as non-moving time.
    /// </summary>
    public static DateTime ClampToNow(DateTime to, DateTime utcNow) =>
        to > utcNow ? utcNow : to;
}
