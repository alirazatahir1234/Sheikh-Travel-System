using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Fills missing / coarse human-readable addresses on replay bundles (first/last playback points and stops).
/// </summary>
public static class TripReplayAddressEnricher
{
    private static bool ContainsNonAsciiLetters(string text) =>
        text.Any(c => char.IsLetter(c) && c > 127);

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
        await EnrichStopsAsync(stops, geocoder, cancellationToken);
        await EnrichEventsAsync(events, geocoder, cancellationToken);

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
        await EnrichStopsAsync(stops, geocoder, cancellationToken);
        await EnrichEventsAsync(events, geocoder, cancellationToken);

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

    /// <summary>
    /// Street/locality first. PlaceName is metadata only when distance-qualified by the geocoder —
    /// do not promote unchecked Nearby POIs into the stored address line.
    /// </summary>
    public static string? FormatResolvedAddress(ReverseGeocodeResult? result)
    {
        if (result is null) return null;
        var formatted = SanitizeFleetAddress(result.FormattedAddress);
        if (!string.IsNullOrWhiteSpace(formatted))
            return formatted;

        // Do not fall back to unchecked PlaceName for fleet operators.
        return null;
    }

    /// <summary>
    /// City/region-only, plus-code, or legacy "Near {POI}" lines that need a street-level refresh.
    /// </summary>
    public static bool IsCoarseAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return true;
        var raw = address.Trim();
        if (ContainsNonAsciiLetters(raw))
            return true;
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("tehsil") || lower.Contains("district") || lower.Contains("division"))
            return true;

        if (raw.StartsWith("Near ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (System.Text.RegularExpressions.Regex.IsMatch(
                raw,
                @"\b[23456789CFGHJMPQRVWX]{4,8}\+[23456789CFGHJMPQRVWX]{2,3}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    /// <summary>Strip legacy Near-prefix and plus-code segments from a stored address line.</summary>
    public static string? SanitizeFleetAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var raw = address.Trim();
        if (raw.StartsWith("Near ", StringComparison.OrdinalIgnoreCase))
        {
            var comma = raw.IndexOf(',');
            raw = comma > 0 ? raw[(comma + 1)..].Trim() : string.Empty;
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !System.Text.RegularExpressions.Regex.IsMatch(
                p,
                @"\b[23456789CFGHJMPQRVWX]{4,8}\+[23456789CFGHJMPQRVWX]{2,3}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .ToList();
        if (parts.Count == 0) return null;
        var cleaned = string.Join(", ", parts);
        return RemoveDiacritics(cleaned);
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).Normalize(System.Text.NormalizationForm.FormC);
    }

    private static async Task EnrichStopsAsync(
        List<TripStopDto> stops,
        IReverseGeocodingService geocoder,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < stops.Count; i++)
        {
            var s = stops[i];
            var coarse = IsCoarseAddress(s.Address);
            if (!coarse) continue;

            var addr = await ResolveAsync(geocoder, s.Latitude, s.Longitude, forceRefresh: coarse && !string.IsNullOrWhiteSpace(s.Address), cancellationToken);
            if (addr is not null)
                stops[i] = s with { Address = addr };
        }
    }

    private static async Task EnrichEventsAsync(
        List<TripEventDto> events,
        IReverseGeocodingService geocoder,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Latitude is null || e.Longitude is null) continue;
            if (!IsCoarseAddress(e.Address)) continue;

            var addr = await ResolveAsync(
                geocoder,
                e.Latitude.Value,
                e.Longitude.Value,
                forceRefresh: !string.IsNullOrWhiteSpace(e.Address),
                cancellationToken);
            if (addr is not null)
                events[i] = e with { Address = addr };
        }
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
            if (!IsCoarseAddress(p.Address)) continue;
            var addr = await ResolveAsync(
                geocoder,
                p.Latitude,
                p.Longitude,
                forceRefresh: !string.IsNullOrWhiteSpace(p.Address),
                cancellationToken);
            if (addr is not null)
                positions[idx] = p with { Address = addr };
        }
    }

    private static async Task<string?> ResolveAsync(
        IReverseGeocodingService geocoder,
        double latitude,
        double longitude,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await geocoder.GetAddressAsync(latitude, longitude, forceRefresh, cancellationToken);
            return FormatResolvedAddress(result);
        }
        catch
        {
            return null;
        }
    }
}
