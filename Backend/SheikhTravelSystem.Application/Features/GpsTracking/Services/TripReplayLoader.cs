using MediatR;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TripReplayLoader
{
    private const int SparseRouteThreshold = 5;

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
        int routeMaxPoints = 2000,
        int playbackMaxPoints = 800)
    {
        var routeTask = traccarClient.GetRouteAsync(traccarDeviceId, fromDate, toDate, cancellationToken);
        var stopsTask = traccarClient.GetStopsAsync(traccarDeviceId, fromDate, toDate, cancellationToken);
        var eventsTask = traccarClient.GetEventsAsync(traccarDeviceId, fromDate, toDate, ct: cancellationToken);
        var summaryTask = traccarClient.GetSummaryAsync(traccarDeviceId, fromDate, toDate, cancellationToken);
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

        var route = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: routeMaxPoints);
        var playback = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: playbackMaxPoints);

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
        CancellationToken cancellationToken)
    {
        var history = await mediator.Send(new Queries.GetPositionHistoryQuery(vehicleId, fromDate, toDate), cancellationToken);
        if (!history.Success || history.Data is null)
        {
            return new TripReplayBundleDto([], [], [], [], null);
        }

        var routeFull = history.Data
            .Select(TripAnalyticsMapper.ToReplayPosition)
            .OrderBy(p => p.Timestamp)
            .ToList();
        var route = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: 2000);
        var playback = TripAnalyticsMapper.DownsampleReplay(routeFull, maxPoints: 800);

        return new TripReplayBundleDto(
            route,
            playback,
            [],
            [],
            TripAnalyticsMapper.BuildReplaySummary([], route));
    }
}
