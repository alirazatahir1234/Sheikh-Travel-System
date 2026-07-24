namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

public class TraccarOptions
{
    public const string SectionName = "Traccar";

    public string BaseUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool Enabled { get; set; } = false;

    /// <summary>Legacy — maps to moving floor when PositionSyncIntervalSeconds / MovingIntervalSeconds unset.</summary>
    public int SyncIntervalSeconds { get; set; } = 10;

    /// <summary>Moving-vehicle floor (also used when AdaptivePositionSync is false).</summary>
    public int PositionSyncIntervalSeconds { get; set; } = 5;

    public int EventSyncIntervalSeconds { get; set; } = 10;
    public int DeviceSyncIntervalSeconds { get; set; } = 300;
    public int GeofenceSyncIntervalSeconds { get; set; } = 1800;
    public int StatisticsSyncIntervalSeconds { get; set; } = 60;

    /// <summary>When true, position sync delay adapts to fleet motion / ignition / SOS.</summary>
    public bool AdaptivePositionSync { get; set; } = true;

    /// <summary>Any vehicle ≥ this speed (km/h) → moving interval.</summary>
    public decimal MovingSpeedKmh { get; set; } = 10m;

    public int MovingIntervalSeconds { get; set; } = 5;
    public int SlowTrafficIntervalSeconds { get; set; } = 15;
    public int IdleIntervalSeconds { get; set; } = 30;
    public int ParkedIntervalSeconds { get; set; } = 300;

    public int ResolvedPositionIntervalSeconds =>
        PositionSyncIntervalSeconds > 0
            ? PositionSyncIntervalSeconds
            : (MovingIntervalSeconds > 0 ? MovingIntervalSeconds : SyncIntervalSeconds);

    /// <summary>
    /// Values of the Traccar position <c>attributes.alarm</c> field treated as an SOS/panic alarm.
    /// </summary>
    public string[] SosAlarmValues { get; set; } = ["sos", "panic"];

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
