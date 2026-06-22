using System.Text.Json;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceReportHelper
{
    public static string NormalizeReportType(string reportType) => reportType.ToLowerInvariant() switch
    {
        "summary" => "cost-analysis",
        "vehicle" => "vehicle-maintenance",
        "overdue" => "overdue-maintenance",
        _ => reportType.ToLowerInvariant()
    };

    public static (DateTime From, DateTime To) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var resolvedFrom = from ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var resolvedTo = to ?? DateTime.UtcNow.Date.AddDays(1);
        return (resolvedFrom, resolvedTo);
    }

    public static string? SerializeFilters(MaintenanceReportFiltersDto filters) =>
        JsonSerializer.Serialize(filters);

    public static MaintenanceReportFiltersDto ParseFilters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new MaintenanceReportFiltersDto(null, null, null, null, null);
        try
        {
            return JsonSerializer.Deserialize<MaintenanceReportFiltersDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new MaintenanceReportFiltersDto(null, null, null, null, null);
        }
        catch (JsonException)
        {
            return new MaintenanceReportFiltersDto(null, null, null, null, null);
        }
    }

    public static DateTime ComputeNextRunAt(string frequency, DateTime? fromUtc = null)
    {
        var baseTime = (fromUtc ?? DateTime.UtcNow).Date.AddHours(6);
        return frequency.ToLowerInvariant() switch
        {
            "daily" => baseTime.AddDays(1),
            "monthly" => baseTime.AddMonths(1),
            _ => baseTime.AddDays(7)
        };
    }

    public static MaintenanceReportRowDto Row(
        string key, string label, int count, decimal totalCost,
        params (string Key, object? Value)[] fields) =>
        new(key, label, count, totalCost,
            fields.ToDictionary(f => f.Key, f => f.Value, StringComparer.OrdinalIgnoreCase));

    public static bool MatchesStatusFilter(string? filter, string actualStatus)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals("All", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(filter, actualStatus, StringComparison.OrdinalIgnoreCase);
    }

    public static string TitleFor(string reportType) => reportType switch
    {
        "vehicle-maintenance" => "Vehicle Maintenance Report",
        "service-due" => "Service Due Report",
        "overdue-maintenance" => "Overdue Maintenance Report",
        "workshop-performance" => "Workshop Performance Report",
        "vendor-performance" => "Vendor Performance Report",
        "cost-analysis" => "Cost Analysis Report",
        "breakdown" => "Breakdown Report",
        _ => "Maintenance Report"
    };
}
