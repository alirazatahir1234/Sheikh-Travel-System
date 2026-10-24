namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Pure rules shared by AuditEngine (testable without DB/HTTP).
/// </summary>
public static class AuditEngineRules
{
    public const int DefaultMaxPageSize = 200;
    public const int ExportMaxPageSize = 10_000;

    public static int ClampPageSize(int pageSize, bool forExport = false) =>
        Math.Clamp(pageSize, 1, forExport ? ExportMaxPageSize : DefaultMaxPageSize);

    /// <summary>
    /// Calendar dates at midnight are treated as inclusive end-of-day so a "To" filter
    /// of 2026-10-24 does not exclude events later that day.
    /// </summary>
    public static DateTime? NormalizeToDateInclusive(DateTime? toDate)
    {
        if (toDate is not DateTime to) return null;
        if (to.TimeOfDay == TimeSpan.Zero)
            return to.Date.AddDays(1).AddTicks(-1);
        return to;
    }

    /// <summary>Returns an error message when From &gt; To; otherwise null.</summary>
    public static string? ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate is DateTime from && toDate is DateTime to && from > to)
            return "From date must be on or before To date.";
        return null;
    }

    /// <summary>
    /// When Stage 13 <c>audit.login_events</c> is false, skip all auth.* events (incl. lockout).
    /// </summary>
    public static bool ShouldSkipAuthEvent(string eventKey, bool loginEventsEnabled)
    {
        if (loginEventsEnabled) return false;
        return eventKey.StartsWith("auth.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads Success from ApiResponse&lt;T&gt; when present; defaults to true for non-ApiResponse.
    /// </summary>
    public static bool ResolveCommandSuccess(object? response)
    {
        if (response is null) return true;
        var type = response.GetType();
        if (!type.IsGenericType) return true;

        var def = type.GetGenericTypeDefinition();
        if (def != typeof(ApiResponse<>)
            && def.FullName?.StartsWith(
                "SheikhTravelSystem.Application.Common.ApiResponse", StringComparison.Ordinal) != true)
            return true;

        var successProp = type.GetProperty("Success");
        return successProp?.GetValue(response) is bool success ? success : true;
    }
}
