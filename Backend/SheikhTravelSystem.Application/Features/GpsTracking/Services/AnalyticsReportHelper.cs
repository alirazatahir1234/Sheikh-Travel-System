using System.Text.Json;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Mirrors MaintenanceReportHelper's conventions for GpsAnalyticsReportSchedules — same NextRunAt cadence logic, same filters JSON serialization approach.</summary>
public static class AnalyticsReportHelper
{
    public static readonly string[] ReportTypes = ["fleet-summary", "driver-performance", "cost"];

    public static string NormalizeReportType(string reportType) => reportType.ToLowerInvariant() switch
    {
        "summary" or "fleet" or "fleet-summary" => "fleet-summary",
        "driver" or "driver-performance" or "drivers" => "driver-performance",
        "cost" or "operational-cost" => "cost",
        _ => reportType.ToLowerInvariant()
    };

    public static string? SerializeFilters(AnalyticsReportFiltersDto filters) =>
        JsonSerializer.Serialize(filters);

    public static AnalyticsReportFiltersDto ParseFilters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AnalyticsReportFiltersDto(null, null, null, null);
        try
        {
            return JsonSerializer.Deserialize<AnalyticsReportFiltersDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new AnalyticsReportFiltersDto(null, null, null, null);
        }
        catch (JsonException)
        {
            return new AnalyticsReportFiltersDto(null, null, null, null);
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
}
