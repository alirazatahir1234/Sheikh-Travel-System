using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Mirrors MaintenanceReportHelper's conventions for GpsAnalyticsReportSchedules — same NextRunAt cadence logic, same filters JSON serialization approach.</summary>
public static class AnalyticsReportHelper
{
    public static readonly string[] ReportTypes = ["fleet-summary", "driver-performance", "cost"];
    public static readonly string[] Frequencies = ["Daily", "Weekly", "Monthly"];

    private static readonly Regex EmailSplit = new(@"[,\s;]+", RegexOptions.Compiled);

    public static string NormalizeReportType(string reportType) => reportType.ToLowerInvariant() switch
    {
        "summary" or "fleet" or "fleet-summary" => "fleet-summary",
        "driver" or "driver-performance" or "drivers" => "driver-performance",
        "cost" or "operational-cost" => "cost",
        _ => reportType.ToLowerInvariant()
    };

    public static string? ValidateScheduleInput(string reportType, string frequency, string recipients)
    {
        var normalizedType = NormalizeReportType(reportType);
        if (!ReportTypes.Contains(normalizedType))
            return "Report type must be fleet-summary, driver-performance, or cost.";

        return ValidateFrequencyAndRecipients(frequency, recipients);
    }

    public static string? ValidateFrequencyAndRecipients(string frequency, string recipients)
    {
        var freq = frequency?.Trim() ?? "";
        if (!Frequencies.Any(f => f.Equals(freq, StringComparison.OrdinalIgnoreCase)))
            return "Frequency must be Daily, Weekly, or Monthly.";

        var emails = ParseRecipients(recipients);
        if (emails.Count == 0)
            return "Add at least one recipient email.";

        foreach (var email in emails)
        {
            if (!IsValidEmail(email))
                return $"Invalid recipient email: {email}";
        }

        return null;
    }

    public static string NormalizeFrequency(string frequency) =>
        Frequencies.First(f => f.Equals(frequency.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string NormalizeRecipients(string recipients) =>
        string.Join(", ", ParseRecipients(recipients));

    public static List<string> ParseRecipients(string? recipients) =>
        string.IsNullOrWhiteSpace(recipients)
            ? []
            : EmailSplit.Split(recipients)
                .Select(e => e.Trim())
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

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

    /// <summary>
    /// Next run at 06:00 local time in <paramref name="timeZone"/> (the tenant's), returned in UTC.
    /// Without a timezone, 06:00 UTC (previous behaviour).
    /// </summary>
    public static DateTime ComputeNextRunAt(string frequency, DateTime? fromUtc = null, TimeZoneInfo? timeZone = null)
    {
        var from = fromUtc ?? DateTime.UtcNow;
        var baseTime = timeZone is null
            ? from.Date.AddHours(6)
            : TenantTimeZoneHelper.LocalDayStartUtc(TenantTimeZoneHelper.UtcToLocalDate(from, timeZone), timeZone).AddHours(6);
        return frequency.ToLowerInvariant() switch
        {
            "daily" => baseTime.AddDays(1),
            "monthly" => baseTime.AddMonths(1),
            _ => baseTime.AddDays(7)
        };
    }
}
