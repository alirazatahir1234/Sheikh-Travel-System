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
        "idle" or "idling" => "idle",
        "stops" => "stop",
        "events" => "event",
        "alerts" => "alert",
        "maintenance-report" => "maintenance",
        _ => reportType.ToLowerInvariant()
    };

    public static (DateTime From, DateTime To) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var resolvedFrom = from ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var resolvedTo = to ?? DateTime.UtcNow.Date.AddDays(1);
        return (resolvedFrom, resolvedTo);
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
        "speed" => "Speed Report",
        "idle" => "Idle Report",
        "stop" => "Stop Report",
        "event" => "Event Report",
        "alert" => "Alert Report",
        "maintenance" => "Maintenance Report",
        _ => "Fleet Report"
    };
}
