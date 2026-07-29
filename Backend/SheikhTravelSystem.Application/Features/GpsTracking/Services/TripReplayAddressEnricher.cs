using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Fills missing human-readable addresses on replay bundles (first/last playback points and stops).
/// </summary>
public static class TripReplayAddressEnricher
{
    public static async Task<TripReplayBundleDto> EnrichAsync(
        TripReplayBundleDto bundle,
        IReverseGeocodingService geocoder,
        CancellationToken cancellationToken)
    {
        var route = bundle.Route.ToList();
        var playback = bundle.Playback.ToList();
        var stops = bundle.Stops.ToList();
        var events = bundle.Events.ToList();

        await EnrichPositionListAsync(route, geocoder, cancellationToken);
        await EnrichPositionListAsync(playback, geocoder, cancellationToken);

        for (var i = 0; i < stops.Count; i++)
        {
            var s = stops[i];
            if (!string.IsNullOrWhiteSpace(s.Address)) continue;
            var addr = await ResolveAsync(geocoder, s.Latitude, s.Longitude, cancellationToken);
            if (addr is not null)
                stops[i] = s with { Address = addr };
        }

        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (!string.IsNullOrWhiteSpace(e.Address) || e.Latitude is null || e.Longitude is null)
                continue;
            var addr = await ResolveAsync(geocoder, e.Latitude.Value, e.Longitude.Value, cancellationToken);
            if (addr is not null)
                events[i] = e with { Address = addr };
        }

        return new TripReplayBundleDto(
            route,
            playback,
            stops,
            events,
            bundle.Summary);
    }

    public static async Task<HistoryReplayBundleDto> EnrichAsync(
        HistoryReplayBundleDto bundle,
        IReverseGeocodingService geocoder,
        CancellationToken cancellationToken)
    {
        var route = bundle.Route.ToList();
        var playback = bundle.Playback.ToList();
        var stops = bundle.Stops.ToList();
        var events = bundle.Events.ToList();

        await EnrichPositionListAsync(route, geocoder, cancellationToken);
        await EnrichPositionListAsync(playback, geocoder, cancellationToken);

        for (var i = 0; i < stops.Count; i++)
        {
            var s = stops[i];
            if (!string.IsNullOrWhiteSpace(s.Address)) continue;
            var addr = await ResolveAsync(geocoder, s.Latitude, s.Longitude, cancellationToken);
            if (addr is not null)
                stops[i] = s with { Address = addr };
        }

        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (!string.IsNullOrWhiteSpace(e.Address) || e.Latitude is null || e.Longitude is null)
                continue;
            var addr = await ResolveAsync(geocoder, e.Latitude.Value, e.Longitude.Value, cancellationToken);
            if (addr is not null)
                events[i] = e with { Address = addr };
        }

        return new HistoryReplayBundleDto(
            route,
            playback,
            stops,
            events,
            bundle.Summary,
            bundle.Statistics,
            bundle.MileageKm,
            bundle.Vehicle);
    }

    private static async Task EnrichPositionListAsync(
        IList<TripReplayPositionDto> positions,
        IReverseGeocodingService geocoder,
        CancellationToken cancellationToken)
    {
        if (positions.Count == 0) return;

        var indices = new[] { 0, positions.Count - 1 }.Distinct();
        foreach (var idx in indices)
        {
            if (idx < 0 || idx >= positions.Count) continue;
            var p = positions[idx];
            if (!string.IsNullOrWhiteSpace(p.Address)) continue;
            var addr = await ResolveAsync(geocoder, p.Latitude, p.Longitude, cancellationToken);
            if (addr is not null)
                positions[idx] = p with { Address = addr };
        }
    }

    private static async Task<string?> ResolveAsync(
        IReverseGeocodingService geocoder,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await geocoder.GetAddressAsync(latitude, longitude, forceRefresh: false, cancellationToken);
            return string.IsNullOrWhiteSpace(result?.FormattedAddress) ? null : result.FormattedAddress.Trim();
        }
        catch
        {
            return null;
        }
    }
}
