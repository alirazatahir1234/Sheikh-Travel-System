namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Per-tracker annual platform subscription (Pakistan list prices). Distinct from fleet Nexus tiers
/// (<see cref="SubscriptionPlanPricing"/>). Hardware and installation are not included.
/// </summary>
public static class TrackerAnnualPlanCatalog
{
    public const string PlanType = "TrackerAnnual";

    public sealed record Entry(
        string PlanCode,
        string DisplayName,
        string Description,
        decimal AnnualAmountPkr,
        int SortOrder,
        bool IsDefault,
        bool Visible,
        string[] FeatureKeys);

    /// <summary>GoTrack Basic / SkyTrack Affordable AMC — live map, geofence, standard alerts.</summary>
    public const decimal EssentialAnnualPkr = 8_000m;

    /// <summary>GoTrack Standard / SkyTrack Economy AMC — SMS alert pack, engine lock workflows.</summary>
    public const decimal PlusAnnualPkr = 9_000m;

    /// <summary>GoTrack Gold / SkyTrack Luxury AMC — premium alerts, share live, voice-ready devices.</summary>
    public const decimal PremiumAnnualPkr = 10_000m;

    public static IReadOnlyList<Entry> All { get; } =
    [
        new(
            PlanCode: "TRACKER_ESSENTIAL",
            DisplayName: "Essential",
            Description: "Single-device live tracking on SheikhGo Nexus — web and mobile, geofences, and standard GPS alerts.",
            AnnualAmountPkr: EssentialAnnualPkr,
            SortOrder: 10,
            IsDefault: false,
            Visible: true,
            FeatureKeys: ["live_map", "geofence", "playback", "standard_alerts"]),
        new(
            PlanCode: "TRACKER_PLUS",
            DisplayName: "Plus",
            Description: "Everything in Essential plus SMS/WhatsApp alert workflows and remote engine lock when the device supports relay output.",
            AnnualAmountPkr: PlusAnnualPkr,
            SortOrder: 20,
            IsDefault: true,
            Visible: true,
            FeatureKeys: ["live_map", "geofence", "playback", "standard_alerts", "sms_alerts", "engine_lock"]),
        new(
            PlanCode: "TRACKER_PREMIUM",
            DisplayName: "Premium",
            Description: "Full alert suite, share-live links, priority support, and voice-listening ready devices where hardware allows.",
            AnnualAmountPkr: PremiumAnnualPkr,
            SortOrder: 30,
            IsDefault: false,
            Visible: true,
            FeatureKeys: ["live_map", "geofence", "playback", "standard_alerts", "sms_alerts", "engine_lock", "share_live", "voice_ready"]),
    ];

    public static Entry Default => All.First(e => e.IsDefault);

    public static Entry Resolve(string? planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
            return Default;

        var key = planCode.Trim();
        return All.FirstOrDefault(e =>
                   string.Equals(e.PlanCode, key, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }

    public static decimal ResolveAnnualAmount(string? planCode)
        => Resolve(planCode).AnnualAmountPkr;
}
