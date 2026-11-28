using MediatR;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TripReplayLoader
{
    private const int SparseRouteThreshold = 5;
    // Tighter defaults on the 4GB Azure VM (API MemoryMax=512M) — callers can still raise via query params.
    private const int DefaultRouteMaxPoints = 1200;
    private const int DefaultPlaybackMaxPoints = 500;
    private const int AbsoluteMaxPoints = 10000;

    private static bool IsOverlayEventType(string type) =>
        type.Contains("geofenceEnter", StringComparison.OrdinalIgnoreCase)
        || type.Contains("geofenceExit", StringComparison.OrdinalIgnoreCase)
        || type.Contains("alarm", StringComparison.OrdinalIgnoreCase)
        || type.Contains("overspeed", StringComparison.OrdinalIgnoreCase)
        || type.Contains("hardBraking", StringComparison.OrdinalIgnoreCase)
        || type.Contains("hardAcceleration", StringComparison.OrdinalIgnoreCase);

    public static async Task<TripReplayBundleDto> LoadFromTraccarAsync(
        ITraccarClient traccarClient,
        int traccarDeviceId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<int, string>? geofenceNames = null,
        int? routeMaxPoints = null,
        int? playbackMaxPoints = null,
        bool includeRaw = false,
        List<TripReplayPositionDto>? fullRouteSink = null,
        bool includeStops = true,
        ReplayTimings? timings = null)
    {
        // Each report is timed for the replay's log line. Traccar works its stops report out from every
        // fix of the window (23 s for a busy week), so a caller with its own stops leaves it out.
        var routeTask = TimedAsync(() => traccarClient.GetRouteAsync(traccarDeviceId, fromDate, toDate, cancellationToken), ms => timings?.SetRoute(ms));
        var stopsTask = includeStops
            ? TimedAsync(() => traccarClient.GetStopsAsync(traccarDeviceId, fromDate, toDate, cancellationToken), ms => timings?.SetStops(ms))
            : Task.FromResult<IReadOnlyList<TraccarStop>>([]);
        var eventsTask = TimedAsync(() => traccarClient.GetEventsAsync(traccarDeviceId, fromDate, toDate, ct: cancellationToken), ms => timings?.SetEvents(ms));
        var summaryTask = TimedAsync(() => traccarClient.GetSummaryAsync(traccarDeviceId, fromDate, toDate, cancellationToken), ms => timings?.SetSummary(ms));
        await Task.WhenAll(routeTask, stopsTask, eventsTask, summaryTask);

        var routeFull = (await routeTask)
            .Select(TripAnalyticsMapper.ToReplayPosition)
            .OrderBy(p => p.Timestamp)
            .ToList();

        if (routeFull.Count < SparseRouteThreshold)
        {
            var positions = await traccarClient.GetPositionsByDeviceAsync(
                traccarDeviceId, fromDate, toDate, cancellationToken);
            var fromPositions = positions
                .Select(TripAnalyticsMapper.ToReplayPosition)
                .OrderBy(p => p.Timestamp)
                .ToList();
            if (fromPositions.Count > routeFull.Count)
            {
                routeFull = fromPositions;
            }
        }

        // Every fix, before thinning: the history timeline is worked out from these.
        fullRouteSink?.Clear();
        fullRouteSink?.AddRange(routeFull);

        var resolvedRouteMax = ResolveMaxPoints(routeMaxPoints, DefaultRouteMaxPoints);
        var resolvedPlaybackMax = ResolveMaxPoints(playbackMaxPoints, DefaultPlaybackMaxPoints);
        var route = includeRaw
            ? routeFull
            : TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: resolvedRouteMax);
        var playback = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: resolvedPlaybackMax);

        var stops = (await stopsTask)
            .Where(s => TripAnalyticsMapper.OverlapsWindow(s.StartTime, s.EndTime, fromDate, toDate))
            .Select(TripAnalyticsMapper.ToStopDto)
            .OrderBy(s => s.StartTime)
            .ToList();

        var events = (await eventsTask)
            .Where(e => e.EventTime >= fromDate && e.EventTime <= toDate)
            .Where(e => IsOverlayEventType(e.Type))
            .Select(e =>
            {
                string? geofenceName = null;
                if (e.GeofenceId is int gid && geofenceNames is not null)
                    geofenceNames.TryGetValue(gid, out geofenceName);
                return TripAnalyticsMapper.ToEventDto(e, geofenceName);
            })
            .OrderBy(e => e.Time)
            .ToList();

        var summaries = (await summaryTask).ToArray();
        var replaySummary = TripAnalyticsMapper.BuildReplaySummary(summaries, route);

        return new TripReplayBundleDto(route, playback, stops, events, replaySummary);
    }

    public static async Task<TripReplayBundleDto> LoadFromLocalHistoryAsync(
        IMediator mediator,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken,
        int? routeMaxPoints = null,
        int? playbackMaxPoints = null,
        bool includeRaw = false,
        List<TripReplayPositionDto>? fullRouteSink = null)
    {
        fullRouteSink?.Clear();
        var history = await mediator.Send(new Queries.GetPositionHistoryQuery(vehicleId, fromDate, toDate), cancellationToken);
        if (!history.Success || history.Data is null)
        {
            return new TripReplayBundleDto([], [], [], [], null);
        }

        var routeFull = history.Data
            .Select(TripAnalyticsMapper.ToReplayPosition)
            .OrderBy(p => p.Timestamp)
            .ToList();
        fullRouteSink?.AddRange(routeFull);
        var resolvedRouteMax = ResolveMaxPoints(routeMaxPoints, DefaultRouteMaxPoints);
        var resolvedPlaybackMax = ResolveMaxPoints(playbackMaxPoints, DefaultPlaybackMaxPoints);
        var route = includeRaw
            ? routeFull
            : TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: resolvedRouteMax);
        var playback = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: resolvedPlaybackMax);

        return new TripReplayBundleDto(
            route,
            playback,
            [],
            [],
            TripAnalyticsMapper.BuildReplaySummary([], route));
    }

    private static async Task<T> TimedAsync<T>(Func<Task<T>> call, Action<long> done)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            return await call();
        }
        finally
        {
            done((long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static int ResolveMaxPoints(int? requested, int fallback)
    {
        if (!requested.HasValue || requested.Value <= 0) return fallback;
        return Math.Min(requested.Value, AbsoluteMaxPoints);
    }
}
