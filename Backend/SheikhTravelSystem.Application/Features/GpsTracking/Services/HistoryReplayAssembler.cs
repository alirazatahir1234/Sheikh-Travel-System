using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>What a History replay was loaded from, before it is put together.</summary>
/// <param name="Bundle">The thinned route and playback, events and summary (no stops: the timeline gives them).</param>
/// <param name="FullRoute">Every fix of the window, before thinning; empty when only the thinned route exists.</param>
/// <param name="FromTraccar">True when the bundle came from Traccar (its summary carries max speed and engine hours).</param>
public sealed record HistoryReplayInput(
    TripReplayBundleDto Bundle,
    IReadOnlyList<TripReplayPositionDto> FullRoute,
    bool FromTraccar,
    int VehicleId,
    DateTime FromDate,
    DateTime ToDate,
    TimeZoneInfo TimeZone,
    DateTime NowUtc,
    TripDeviceContextDto? Vehicle,
    int? TraccarDeviceId,
    string? DeviceName);

/// <summary>
/// Puts a History replay together from what was loaded, without waiting for anything outside:
/// <list type="number">
/// <item>the timeline, cut from every fix (<see cref="HistoryTimelineBuilder"/>);</item>
/// <item>its places, named from the address cache; unknown places are queued for a background lookup;</item>
/// <item>the stops: the timeline's standstills (<see cref="HistoryTimelineBuilder.ToStops"/>), so the
/// stops list, stop and parking counts and the timeline agree, in the ERP and in the mobile app;</item>
/// <item>the route ends and events, named from the cache;</item>
/// <item>statistics from the route and those stops, then the display fields.</item>
/// </list>
/// If the timeline cannot be built the replay is still returned without it (the History page falls
/// back to the legacy strip) and the stops come from <see cref="ReplayStopDetector"/> over every fix.
/// </summary>
public static class HistoryReplayAssembler
{
    public static async Task<HistoryReplayBundleDto> AssembleAsync(
        HistoryReplayInput input,
        IReverseGeocodingService geocoder,
        IGpsAddressLookupQueue lookups,
        ILogger logger,
        CancellationToken cancellationToken,
        ReplayTimings? timings = null)
    {
        timings ??= new ReplayTimings();
        var bundle = input.Bundle;
        var fixes = input.FullRoute.Count > 0 ? input.FullRoute : bundle.Route;

        var started = Stopwatch.GetTimestamp();
        HistoryTimelineDto? timeline = null;
        try
        {
            timeline = HistoryTimelineBuilder.Build(fixes, input.FromDate, input.ToDate, input.TimeZone, input.NowUtc);
            timings.TimelineMs = ElapsedMs(started);
            started = Stopwatch.GetTimestamp();
            timeline = await HistoryTimelineBuilder.WithAddressesAsync(
                timeline, geocoder, cancellationToken, knownPlaces: null, lookups, timings.Addresses);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "History timeline could not be built for vehicle {VehicleId}", input.VehicleId);
            timeline = null;
            started = Stopwatch.GetTimestamp();
        }

        var stops = timeline is not null
            ? HistoryTimelineBuilder.ToStops(timeline)
            : fixes.Count >= 2 ? ReplayStopDetector.Detect(fixes, input.FromDate, input.ToDate) : [];
        bundle = await TripReplayAddressEnricher.EnrichFromCacheAsync(
            bundle with { Stops = stops },
            geocoder,
            lookups,
            cancellationToken,
            includeStops: timeline is null,
            timings.Addresses);
        timings.AddressesMs = ElapsedMs(started);

        var mileageKm = TripAnalyticsMapper.ComputeOdometerMileageKm(bundle.Route)
            ?? bundle.Summary?.DistanceKm;

        var statistics = TripAnalyticsMapper.BuildHistoryStatistics(
            null,
            bundle.Route,
            bundle.Stops,
            input.FromDate,
            input.ToDate,
            mileageKm,
            // Only a Traccar summary carries real max speed / engine hours; the local
            // BuildReplaySummary derives engine hours from the route span.
            traccarMaxSpeedKmh: input.FromTraccar ? bundle.Summary?.MaxSpeedKmh : null,
            traccarEngineHours: input.FromTraccar ? bundle.Summary?.EngineHours : null);

        // Keep replay summary driving time consistent with clamped history stats.
        var summary = bundle.Summary is null
            ? null
            : bundle.Summary with
            {
                DistanceKm = mileageKm ?? bundle.Summary.DistanceKm,
                DrivingMinutes = statistics.DrivingMinutes,
                AvgSpeedKmh = statistics.AvgSpeedKmh,
                MaxSpeedKmh = statistics.MaxSpeedKmh,
                EngineHours = statistics.EngineHours
            };

        return HistoryReplayMapper.WithDisplayFields(
            new HistoryReplayBundleDto(
                bundle.Route,
                bundle.Playback,
                bundle.Stops,
                bundle.Events,
                summary,
                statistics,
                mileageKm,
                input.Vehicle),
            input.TraccarDeviceId,
            input.DeviceName,
            input.FromDate,
            input.ToDate) with { Timeline = timeline };
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
