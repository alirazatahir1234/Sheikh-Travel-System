namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Where one History replay build spent its time, for its log line. The Traccar reports run in
/// parallel, so they are set from their own tasks.
/// </summary>
public sealed class ReplayTimings
{
    private long _routeMs;
    private long _stopsMs = -1;
    private long _eventsMs;
    private long _summaryMs;

    public long RouteMs => Interlocked.Read(ref _routeMs);

    /// <summary>-1 when the stops report was not requested.</summary>
    public long StopsMs => Interlocked.Read(ref _stopsMs);

    public long EventsMs => Interlocked.Read(ref _eventsMs);
    public long SummaryMs => Interlocked.Read(ref _summaryMs);

    /// <summary>The whole Traccar (or local history) load, including any fallback.</summary>
    public long LoadMs { get; set; }

    public long TimelineMs { get; set; }

    /// <summary>Naming the timeline, the route ends and the events from the address cache.</summary>
    public long AddressesMs { get; set; }

    public AddressLookupTally Addresses { get; } = new();

    internal void SetRoute(long ms) => Interlocked.Exchange(ref _routeMs, ms);
    internal void SetStops(long ms) => Interlocked.Exchange(ref _stopsMs, ms);
    internal void SetEvents(long ms) => Interlocked.Exchange(ref _eventsMs, ms);
    internal void SetSummary(long ms) => Interlocked.Exchange(ref _summaryMs, ms);
}
