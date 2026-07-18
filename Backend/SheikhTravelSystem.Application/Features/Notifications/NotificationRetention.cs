using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Notifications;

/// <summary>Retention category buckets used by lifecycle jobs and create-time classification.</summary>
public static class NotificationRetention
{
    public const string Standard = "Standard";
    public const string Operational = "Operational";
    public const string Maintenance = "Maintenance";
    public const string Compliance = "Compliance";
    public const string Critical = "Critical";
    public const string Security = "Security";
    public const string Ai = "Ai";

    public const string SettingsCategory = "NotificationRetention";

    public static (string Category, bool NeverAutoDelete) Classify(
        NotificationType type,
        string? module,
        string? templateKey,
        string? title = null)
    {
        var key = templateKey?.Trim().ToLowerInvariant() ?? "";
        var mod = module?.Trim() ?? "System";
        var t = title ?? "";

        if (type == NotificationType.Sos
            || key is "sos_alert"
            || ContainsAny(t, "sos", "panic", "theft", "accident"))
            return (Critical, true);

        if (string.Equals(mod, "Security", StringComparison.OrdinalIgnoreCase)
            || key.Contains("login", StringComparison.Ordinal)
            || key.Contains("security", StringComparison.Ordinal))
            return (Security, false);

        if (string.Equals(mod, "Compliance", StringComparison.OrdinalIgnoreCase)
            || key.Contains("compliance", StringComparison.Ordinal)
            || key.Contains("license", StringComparison.Ordinal)
            || key.Contains("insurance", StringComparison.Ordinal))
            return (Compliance, false);

        if (string.Equals(mod, "Maintenance", StringComparison.OrdinalIgnoreCase)
            || key.Contains("maintenance", StringComparison.Ordinal))
            return (Maintenance, false);

        if (type == NotificationType.VehicleOffline
            || key is "vehicle_offline" or "speed_alert"
            || key.Contains("geofence", StringComparison.Ordinal)
            || key.Contains("battery", StringComparison.Ordinal)
            || key.Contains("fuel", StringComparison.Ordinal)
            || string.Equals(mod, "Fleet", StringComparison.OrdinalIgnoreCase))
            return (Operational, false);

        if (string.Equals(mod, "Communication", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("ai_", StringComparison.Ordinal)
            || key.Contains("digest", StringComparison.Ordinal)
            || key.Contains("summary", StringComparison.Ordinal))
            return (Ai, false);

        return (Standard, false);
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public sealed class NotificationRetentionPolicy
{
    public int ReadArchiveDays { get; set; } = 30;
    public int ArchivedDeleteDays { get; set; } = 180;
    public int FailedDeleteDays { get; set; } = 90;
    public int DraftDeleteDays { get; set; } = 30;
    public int OperationalDeleteDays { get; set; } = 90;
    public int MaintenanceDeleteDays { get; set; } = 730;
    public int ComplianceDeleteDays { get; set; } = 2555;
    public bool CriticalNeverDelete { get; set; } = true;
    public int SecurityDeleteDays { get; set; } = 730;

    public int DeleteDaysForCategory(string? category) =>
        (category ?? NotificationRetention.Standard) switch
        {
            NotificationRetention.Operational => OperationalDeleteDays,
            NotificationRetention.Maintenance => MaintenanceDeleteDays,
            NotificationRetention.Compliance => ComplianceDeleteDays,
            NotificationRetention.Security => SecurityDeleteDays,
            NotificationRetention.Ai => Math.Min(ArchivedDeleteDays, 180),
            NotificationRetention.Critical => CriticalNeverDelete ? int.MaxValue : ArchivedDeleteDays,
            _ => ArchivedDeleteDays
        };

    public static NotificationRetentionPolicy FromDictionary(IReadOnlyDictionary<string, string?> values)
    {
        var p = new NotificationRetentionPolicy();
        if (TryInt(values, "ReadArchiveDays", out var v)) p.ReadArchiveDays = v;
        if (TryInt(values, "ArchivedDeleteDays", out v)) p.ArchivedDeleteDays = v;
        if (TryInt(values, "FailedDeleteDays", out v)) p.FailedDeleteDays = v;
        if (TryInt(values, "DraftDeleteDays", out v)) p.DraftDeleteDays = v;
        if (TryInt(values, "OperationalDeleteDays", out v)) p.OperationalDeleteDays = v;
        if (TryInt(values, "MaintenanceDeleteDays", out v)) p.MaintenanceDeleteDays = v;
        if (TryInt(values, "ComplianceDeleteDays", out v)) p.ComplianceDeleteDays = v;
        if (TryInt(values, "SecurityDeleteDays", out v)) p.SecurityDeleteDays = v;
        if (values.TryGetValue("CriticalNeverDelete", out var c) && bool.TryParse(c, out var never))
            p.CriticalNeverDelete = never;
        return p;
    }

    public Dictionary<string, string?> ToDictionary() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ReadArchiveDays"] = ReadArchiveDays.ToString(),
        ["ArchivedDeleteDays"] = ArchivedDeleteDays.ToString(),
        ["FailedDeleteDays"] = FailedDeleteDays.ToString(),
        ["DraftDeleteDays"] = DraftDeleteDays.ToString(),
        ["OperationalDeleteDays"] = OperationalDeleteDays.ToString(),
        ["MaintenanceDeleteDays"] = MaintenanceDeleteDays.ToString(),
        ["ComplianceDeleteDays"] = ComplianceDeleteDays.ToString(),
        ["CriticalNeverDelete"] = CriticalNeverDelete.ToString().ToLowerInvariant(),
        ["SecurityDeleteDays"] = SecurityDeleteDays.ToString()
    };

    private static bool TryInt(IReadOnlyDictionary<string, string?> values, string key, out int value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw)
               && int.TryParse(raw, out value)
               && value >= 0;
    }
}
