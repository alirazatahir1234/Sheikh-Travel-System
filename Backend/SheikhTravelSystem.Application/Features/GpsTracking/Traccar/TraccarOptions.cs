namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

public class TraccarOptions
{
    public const string SectionName = "Traccar";

    public string BaseUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool Enabled { get; set; } = false;

    /// <summary>Legacy — maps to position interval when PositionSyncIntervalSeconds is unset.</summary>
    public int SyncIntervalSeconds { get; set; } = 10;

    public int PositionSyncIntervalSeconds { get; set; } = 5;
    public int EventSyncIntervalSeconds { get; set; } = 10;
    public int DeviceSyncIntervalSeconds { get; set; } = 300;
    public int GeofenceSyncIntervalSeconds { get; set; } = 1800;
    public int StatisticsSyncIntervalSeconds { get; set; } = 60;

    public int ResolvedPositionIntervalSeconds =>
        PositionSyncIntervalSeconds > 0 ? PositionSyncIntervalSeconds : SyncIntervalSeconds;

    /// <summary>True when BaseUrl resolves to a valid absolute URI (scheme + host).</summary>
    public bool IsConfigured => TryGetBaseUri(out _);

    /// <summary>
    /// Normalizes Traccar:BaseUrl (adds http:// when scheme omitted) and returns a trailing-slash base URI.
    /// </summary>
    public bool TryGetBaseUri(out Uri? baseUri)
    {
        baseUri = null;
        if (string.IsNullOrWhiteSpace(BaseUrl))
            return false;

        var normalized = BaseUrl.Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal))
            normalized = "http://" + normalized;

        if (!Uri.TryCreate(normalized.TrimEnd('/') + "/", UriKind.Absolute, out var parsed))
            return false;

        if (string.IsNullOrWhiteSpace(parsed.Host))
            return false;

        baseUri = parsed;
        return true;
    }
}
