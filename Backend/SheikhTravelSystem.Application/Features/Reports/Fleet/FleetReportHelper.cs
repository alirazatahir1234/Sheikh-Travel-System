using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public static class FleetReportHelper
{
    public static string NormalizeReportType(string reportType) => reportType.ToLowerInvariant() switch
    {
        "trips" => "trip",
        "vehicles" => "vehicle",
        "drivers" => "driver",
        "fuel-report" => "fuel",
        "fuel-efficiency-report" => "fuel-efficiency",
        "idle" or "idling" => "idle",
        "stops" => "stop",
        "events" => "event",
        "alerts" => "alert",
        "maintenance-report" => "maintenance",
        _ => reportType.ToLowerInvariant()
    };

    /// <summary>
    /// Resolves an inclusive calendar date range into a half-open [from, toExclusive) window.
    /// Date-only "to" values (midnight) are treated as that calendar day inclusive.
    /// </summary>
    /// <param name="timeZone">
    /// Tenant timezone for the defaults only (this month / through today in the tenant's calendar).
    /// Explicit <paramref name="from"/>/<paramref name="to"/> values are used as sent.
    /// </param>
    public static (DateTime From, DateTime ToExclusive) ResolveDateRange(
        DateTime? from, DateTime? to, TimeZoneInfo? timeZone = null)
    {
        var today = timeZone is null
            ? DateTime.UtcNow.Date
            : DateTime.SpecifyKind(
                TenantTimeZoneHelper.UtcToLocalDate(DateTime.UtcNow, timeZone).ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);
        var resolvedFrom = from?.Date
            ?? new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        DateTime toExclusive;
        if (!to.HasValue)
        {
            toExclusive = today.AddDays(1);
        }
        else if (to.Value.TimeOfDay == TimeSpan.Zero)
        {
            // Frontend date pickers send YYYY-MM-DD → midnight; include the full day.
            toExclusive = to.Value.Date.AddDays(1);
        }
        else
        {
            toExclusive = to.Value;
        }

        if (toExclusive <= resolvedFrom)
            toExclusive = resolvedFrom.AddDays(1);

        return (resolvedFrom, toExclusive);
    }

    public static ReportRowDto Row(
        string key, string label, int count, decimal totalValue,
        params (string Key, object? Value)[] fields) =>
        new(key, label, count, totalValue,
            fields.ToDictionary(f => f.Key, f => f.Value, StringComparer.OrdinalIgnoreCase));

    public static bool MatchesStatusFilter(string? filter, string actualStatus)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals("All", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(filter, actualStatus, StringComparison.OrdinalIgnoreCase);
    }

    public static string TitleFor(string reportType) => reportType switch
    {
        "trip" => "Trip Report",
        "vehicle" => "Vehicle Report",
        "driver" => "Driver Report",
        "fuel" => "Fuel Report",
        "fuel-efficiency" => "Fuel Efficiency (Km/L)",
        "speed" => "Speed Report",
        "idle" => "Idle Report",
        "stop" => "Stop Report",
        "event" => "Event Report",
        "alert" => "Alert Report",
        "maintenance" => "Maintenance Report",
        _ => "Fleet Report"
    };
}
