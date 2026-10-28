namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

/// <summary>
/// Product policy for Smart Trip Assignment (Module 4). Auto-apply stays off until
/// a tenant completes ~2 weeks of suggest/shadow observation.
/// </summary>
public static class SmartAssignmentPolicy
{
    /// <summary>Default operating mode — staff must confirm every recommendation.</summary>
    public const string ModeSuggestOnly = "SuggestOnly";

    /// <summary>Log “would assign” without writing trip driver/vehicle.</summary>
    public const string ModeShadow = "Shadow";

    /// <summary>Tenant-gated auto-apply (off by default; not enabled in this ship).</summary>
    public const string ModeAutoApply = "AutoApply";

    /// <summary>Active mode until tenant AutoApply is explicitly turned on.</summary>
    public static string ActiveMode { get; set; } = ModeSuggestOnly;

    /// <summary>Create-trip must never assign without staff confirm.</summary>
    public static bool CreateTimeAutoAssignEnabled => false;

    /// <summary>One-click / API auto-apply without a selected candidate.</summary>
    public static bool AutoApplyEnabled =>
        string.Equals(ActiveMode, ModeAutoApply, StringComparison.OrdinalIgnoreCase);

    /// <summary>When true, suggestions also persist a Shadow decision for the #1 candidate.</summary>
    public static bool ShadowLoggingEnabled =>
        string.Equals(ActiveMode, ModeShadow, StringComparison.OrdinalIgnoreCase)
        || ShadowLoggingForceEnabled;

    /// <summary>Design support for ~2-week shadow period without flipping ActiveMode.</summary>
    public static bool ShadowLoggingForceEnabled { get; set; }

    /// <summary>EV range must cover trip distance × this factor (15% buffer).</summary>
    public const double EvRangeBufferFactor = 1.15;

    /// <summary>Hard daily hours limit — candidates at/above are excluded (not soft-scored).</summary>
    public const decimal MaxHoursWorkedHardLimit = 12m;
}
