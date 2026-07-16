namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Read-layer normalization for GpsAlertEvents.EventType spelling variants — detectors write
/// synonyms (speed_exceeded vs overspeed, vehicle_offline/device_offline vs offline, etc.; see
/// GpsAlertWriter.SeverityFor and AlertTypeCatalog.Types) so a "count by event type" GROUP BY would
/// otherwise split one real event family into multiple buckets. Canonical spelling = AlertTypeCatalog
/// since that's already user-facing (alert settings matrix). Fixed here rather than at the detector
/// source since touching Phase 8 write paths broadly is a bigger, separate change.
/// </summary>
public static class GpsEventTypeNormalizer
{
    private static readonly Dictionary<string, string> ToCanonical = BuildLookup();

    public static string Normalize(string eventType) =>
        ToCanonical.TryGetValue(eventType, out var canonical) ? canonical : eventType;

    private static Dictionary<string, string> BuildLookup()
    {
        var families = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["overspeed"] = ["overspeed", "speed_exceeded"],
            ["offline"] = ["offline", "vehicle_offline", "device_offline"],
            ["online"] = ["online", "vehicle_online", "device_online"],
        };

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (canonical, variants) in families)
        {
            foreach (var variant in variants)
            {
                lookup[variant] = canonical;
            }
        }

        return lookup;
    }
}
