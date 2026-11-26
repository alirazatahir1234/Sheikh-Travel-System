namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Platform-wide localization defaults (section <c>Localization</c>). Tenants and branches keep their own
/// configured timezone; this default applies only where none is set (new tenants, branches, fallbacks).
/// </summary>
public sealed class LocalizationOptions
{
    public const string SectionName = "Localization";

    /// <summary>Used only when configuration is missing — configure <c>Localization:DefaultTimeZone</c>.</summary>
    public const string FallbackTimeZoneId = "Asia/Karachi";

    /// <summary>IANA timezone id for new tenants/branches and for tenants with no timezone set.</summary>
    public string DefaultTimeZone { get; set; } = FallbackTimeZoneId;

    /// <summary>Configured default, or the fallback when the configured value is blank.</summary>
    public string EffectiveDefaultTimeZone =>
        string.IsNullOrWhiteSpace(DefaultTimeZone) ? FallbackTimeZoneId : DefaultTimeZone.Trim();
}
