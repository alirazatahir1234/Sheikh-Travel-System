using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Reverse geocoder: GpsAddressCache → Google Places/Geocoding (when keyed) → Nominatim.
/// Returns street-level address plus nearest shop/POI name when available.
/// </summary>
public sealed class NominatimReverseGeocodingService(
    IDbConnectionFactory dbFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<GeocodingOptions> options,
    ILogger<NominatimReverseGeocodingService> logger) : IReverseGeocodingService
{
    private const int CoordinateDecimals = 4;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private static readonly HashSet<string> AdminOnlyTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "tehsil", "district", "division", "province", "region", "country",
        "pakistan", "india", "punjab", "sindh", "balochistan", "khyber"
    };

    private static readonly HashSet<string> IgnoredPlaceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "locality", "political", "country", "administrative_area_level_1",
        "administrative_area_level_2", "administrative_area_level_3",
        "administrative_area_level_4", "sublocality", "route", "plus_code"
    };

    private readonly object _throttleLock = new();
    private DateTime _nextAllowedCallUtc = DateTime.MinValue;

    public async Task<ReverseGeocodeResult?> GetAddressAsync(
        double latitude,
        double longitude,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return null;

        var latKey = Math.Round(latitude, CoordinateDecimals, MidpointRounding.AwayFromZero);
        var lngKey = Math.Round(longitude, CoordinateDecimals, MidpointRounding.AwayFromZero);

        using var connection = dbFactory.CreateConnection();

        if (!forceRefresh)
        {
            var cached = await connection.QuerySingleOrDefaultAsync<CachedRow>(new CommandDefinition("""
                SELECT TOP 1 Address, City, State, Country, PostalCode, Road, PlaceName, PlaceType, ResolvedAt
                FROM GpsAddressCache
                WHERE LatitudeKey = @LatKey AND LongitudeKey = @LngKey
                """,
                new { LatKey = latKey, LngKey = lngKey },
                cancellationToken: cancellationToken));

            if (cached is not null
                && !string.IsNullOrWhiteSpace(cached.Address)
                && DateTime.UtcNow - cached.ResolvedAt < CacheTtl
                && !IsCoarseAddress(cached.Address, cached.Road, cached.PlaceName))
            {
                return new ReverseGeocodeResult(
                    cached.Address,
                    cached.Road,
                    cached.City,
                    cached.State,
                    cached.Country,
                    cached.PostalCode,
                    FromCache: true,
                    cached.PlaceName,
                    cached.PlaceType);
            }
        }

        ReverseGeocodeResult? resolved = null;
        if (!string.IsNullOrWhiteSpace(options.Value.GoogleMapsApiKey))
        {
            resolved = await ResolveFromGoogleAsync(latitude, longitude, cancellationToken);
        }

        if (resolved is null || IsCoarseAddress(resolved.FormattedAddress, resolved.Road, resolved.PlaceName))
        {
            var nominatim = await ResolveFromNominatimAsync(latitude, longitude, cancellationToken);
            if (nominatim is not null)
            {
                resolved = MergeResults(resolved, nominatim);
            }
        }

        if (resolved is null || string.IsNullOrWhiteSpace(resolved.FormattedAddress))
            return null;

        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE GpsAddressCache AS target
            USING (SELECT @LatKey AS LatitudeKey, @LngKey AS LongitudeKey) AS source
            ON target.LatitudeKey = source.LatitudeKey AND target.LongitudeKey = source.LongitudeKey
            WHEN MATCHED THEN UPDATE SET
                Address = @Address, Road = @Road, City = @City, State = @State,
                Country = @Country, PostalCode = @PostalCode,
                PlaceName = @PlaceName, PlaceType = @PlaceType, ResolvedAt = @ResolvedAt
            WHEN NOT MATCHED THEN INSERT
                (LatitudeKey, LongitudeKey, Address, Road, City, State, Country, PostalCode, PlaceName, PlaceType, ResolvedAt)
                VALUES (@LatKey, @LngKey, @Address, @Road, @City, @State, @Country, @PostalCode, @PlaceName, @PlaceType, @ResolvedAt);
            """,
            new
            {
                LatKey = latKey,
                LngKey = lngKey,
                Address = resolved.FormattedAddress,
                resolved.Road,
                resolved.City,
                resolved.State,
                resolved.Country,
                resolved.PostalCode,
                resolved.PlaceName,
                resolved.PlaceType,
                ResolvedAt = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));

        return resolved with { FromCache = false };
    }

    /// <summary>
    /// City/admin-only lines (no road, neighbourhood, or place) — not useful for fleet ops.
    /// </summary>
    internal static bool IsCoarseAddress(string? address, string? road, string? placeName = null)
    {
        if (!string.IsNullOrWhiteSpace(placeName)) return false;
        if (!string.IsNullOrWhiteSpace(road) && !LooksLikeAdminToken(road)) return false;
        if (string.IsNullOrWhiteSpace(address)) return true;

        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        var hasDigit = parts.Any(p => p.Any(char.IsDigit));
        if (hasDigit) return false;

        // "Pasrur, Pasrur Tehsil, Sialkot District, …" — admin hierarchy only.
        var adminHeavy = parts.Count(LooksLikeAdminToken);
        if (adminHeavy >= Math.Max(2, parts.Length - 1)) return true;

        return parts.Length <= 3;
    }

    private static bool LooksLikeAdminToken(string part)
    {
        var lower = part.Trim().ToLowerInvariant();
        if (AdminOnlyTokens.Contains(lower)) return true;
        return AdminOnlyTokens.Any(t => lower.Contains(t, StringComparison.Ordinal));
    }

    private static ReverseGeocodeResult MergeResults(ReverseGeocodeResult? preferred, ReverseGeocodeResult fallback)
    {
        if (preferred is null) return fallback;

        var place = FirstNonEmpty(preferred.PlaceName, fallback.PlaceName);
        var placeType = !string.IsNullOrWhiteSpace(preferred.PlaceName)
            ? preferred.PlaceType
            : FirstNonEmpty(preferred.PlaceType, fallback.PlaceType);
        var road = FirstNonEmpty(preferred.Road, fallback.Road);
        var city = FirstNonEmpty(preferred.City, fallback.City);
        var state = FirstNonEmpty(preferred.State, fallback.State);
        var country = FirstNonEmpty(preferred.Country, fallback.Country);
        var postal = FirstNonEmpty(preferred.PostalCode, fallback.PostalCode);

        var address = preferred.FormattedAddress;
        if (IsCoarseAddress(address, preferred.Road, preferred.PlaceName)
            && !IsCoarseAddress(fallback.FormattedAddress, fallback.Road, fallback.PlaceName))
        {
            address = fallback.FormattedAddress;
        }

        return new ReverseGeocodeResult(address, road, city, state, country, postal, false, place, placeType);
    }

    private async Task<ReverseGeocodeResult?> ResolveFromGoogleAsync(
        double latitude, double longitude, CancellationToken cancellationToken)
    {
        var key = options.Value.GoogleMapsApiKey;
        if (string.IsNullOrWhiteSpace(key)) return null;

        try
        {
            var client = httpClientFactory.CreateClient("GoogleMaps");
            var lat = latitude.ToString(CultureInfo.InvariantCulture);
            var lng = longitude.ToString(CultureInfo.InvariantCulture);

            var nearbyTask = client.GetFromJsonAsync<GoogleNearbyResponse>(
                $"/maps/api/place/nearbysearch/json?location={lat},{lng}&radius=100&language=en&key={key}",
                cancellationToken);
            var geoTask = client.GetFromJsonAsync<GoogleGeocodeResponse>(
                $"/maps/api/geocode/json?latlng={lat},{lng}&language=en&key={key}",
                cancellationToken);

            await Task.WhenAll(nearbyTask, geoTask);
            var nearby = await nearbyTask;
            var geo = await geoTask;

            string? placeName = null;
            string? placeType = null;
            if (nearby?.Status == "OK" && nearby.Results is { Count: > 0 })
            {
                foreach (var place in nearby.Results)
                {
                    if (string.IsNullOrWhiteSpace(place.Name)) continue;
                    var types = place.Types ?? [];
                    if (types.Any(t => IgnoredPlaceTypes.Contains(t))) continue;
                    if (types.Contains("locality", StringComparer.OrdinalIgnoreCase)) continue;
                    placeName = place.Name.Trim();
                    placeType = types.FirstOrDefault(t => !IgnoredPlaceTypes.Contains(t)) ?? types.FirstOrDefault();
                    break;
                }
            }

            string? formatted = null;
            string? road = null;
            string? city = null;
            string? state = null;
            string? country = null;
            string? postal = null;

            if (geo?.Status == "OK" && geo.Results is { Count: > 0 })
            {
                var best = PickBestGeocodeResult(geo.Results);
                if (best is not null)
                {
                    formatted = best.FormattedAddress?.Trim();
                    foreach (var c in best.AddressComponents ?? [])
                    {
                        var types = c.Types ?? [];
                        if (types.Contains("street_number") || types.Contains("route"))
                        {
                            road = string.Join(" ", new[] { road, c.LongName }
                                .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                        }
                        if (types.Contains("locality") || types.Contains("postal_town"))
                            city ??= c.LongName;
                        if (types.Contains("administrative_area_level_1"))
                            state ??= c.LongName;
                        if (types.Contains("country"))
                            country ??= c.LongName;
                        if (types.Contains("postal_code"))
                            postal ??= c.LongName;
                        if (types.Contains("sublocality") || types.Contains("neighborhood"))
                        {
                            // Prefer neighbourhood in formatted line via Geocode formatted_address.
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(formatted) && string.IsNullOrWhiteSpace(placeName))
                return null;

            formatted ??= string.Join(", ", new[] { road, city, state, country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            // Prefer readable street over plus-code-only lines when we have components.
            if (!string.IsNullOrWhiteSpace(formatted)
                && formatted.Contains('+', StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(road))
            {
                var rebuilt = string.Join(", ", new[] { road, city, state, country }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(rebuilt))
                    formatted = rebuilt;
            }

            return new ReverseGeocodeResult(
                formatted!,
                FirstNonEmpty(road),
                city,
                state,
                country,
                postal,
                false,
                placeName,
                placeType);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Google reverse geocode failed for {Lat},{Lng}", latitude, longitude);
            return null;
        }
    }

    private static GoogleGeocodeResult? PickBestGeocodeResult(IReadOnlyList<GoogleGeocodeResult> results)
    {
        static int Score(GoogleGeocodeResult r)
        {
            var types = r.Types ?? [];
            if (types.Contains("street_address")) return 100;
            if (types.Contains("premise")) return 90;
            if (types.Contains("route")) return 80;
            if (types.Contains("establishment") || types.Contains("point_of_interest")) return 70;
            if (types.Contains("plus_code")) return 20;
            return 40;
        }

        return results.OrderByDescending(Score).FirstOrDefault();
    }

    private async Task<ReverseGeocodeResult?> ResolveFromNominatimAsync(
        double latitude, double longitude, CancellationToken cancellationToken)
    {
        TimeSpan wait;
        lock (_throttleLock)
        {
            wait = _nextAllowedCallUtc - DateTime.UtcNow;
            if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
            _nextAllowedCallUtc = DateTime.UtcNow.Add(ThrottleInterval) + wait;
        }

        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, cancellationToken);

        var client = httpClientFactory.CreateClient("Nominatim");
        var url =
            $"/reverse?format=jsonv2&lat={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}" +
            "&zoom=18&addressdetails=1&namedetails=1&extratags=1&accept-language=en";

        try
        {
            var response = await client.GetFromJsonAsync<NominatimReverseResponse>(url, cancellationToken);
            if (response is null || string.IsNullOrWhiteSpace(response.DisplayName))
                return null;

            var a = response.Address;
            var house = a?.HouseNumber;
            var road = FirstNonEmpty(
                a?.Road, a?.Pedestrian, a?.Path, a?.Residential, a?.Street);
            var area = FirstNonEmpty(
                a?.Neighbourhood, a?.Suburb, a?.Quarter, a?.CityDistrict,
                a?.Hamlet, a?.Locality);
            var city = FirstNonEmpty(
                a?.City, a?.Town, a?.Village, a?.Municipality, a?.County);
            var state = FirstNonEmpty(a?.State, a?.Province, a?.Region);
            var country = a?.Country;
            var postal = a?.Postcode;

            var placeName = FirstNonEmpty(
                response.Name,
                a?.Amenity, a?.Shop, a?.Building, a?.Tourism, a?.Office, a?.Leisure,
                a?.Craft, a?.Historic);

            // Unnamed highway → don't treat empty name as a road label.
            if (string.IsNullOrWhiteSpace(road) && response.Addresstype == "road"
                && !string.IsNullOrWhiteSpace(response.Name))
            {
                road = response.Name;
            }

            var street = string.Join(" ", new[] { house, road }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            var shortLine = string.Join(", ", new[] { street, area, city, state, country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            string formatted;
            if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(area))
                formatted = shortLine;
            else if (!string.IsNullOrWhiteSpace(response.DisplayName)
                     && !IsCoarseAddress(response.DisplayName, road: null, placeName))
                formatted = response.DisplayName.Trim();
            else
                formatted = string.IsNullOrWhiteSpace(shortLine)
                    ? response.DisplayName!.Trim()
                    : shortLine;

            return new ReverseGeocodeResult(
                formatted,
                FirstNonEmpty(street, road, area),
                city,
                state,
                country,
                postal,
                false,
                placeName,
                FirstNonEmpty(response.Category, response.Type));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Nominatim reverse geocode failed for {Lat},{Lng}", latitude, longitude);
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private sealed record CachedRow(
        string Address,
        string? City,
        string? State,
        string? Country,
        string? PostalCode,
        string? Road,
        string? PlaceName,
        string? PlaceType,
        DateTime ResolvedAt);

    private sealed record NominatimReverseResponse(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("addresstype")] string? Addresstype,
        [property: JsonPropertyName("address")] NominatimAddress? Address);

    private sealed record NominatimAddress(
        [property: JsonPropertyName("house_number")] string? HouseNumber,
        [property: JsonPropertyName("road")] string? Road,
        [property: JsonPropertyName("street")] string? Street,
        [property: JsonPropertyName("pedestrian")] string? Pedestrian,
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("residential")] string? Residential,
        [property: JsonPropertyName("neighbourhood")] string? Neighbourhood,
        [property: JsonPropertyName("suburb")] string? Suburb,
        [property: JsonPropertyName("quarter")] string? Quarter,
        [property: JsonPropertyName("city_district")] string? CityDistrict,
        [property: JsonPropertyName("hamlet")] string? Hamlet,
        [property: JsonPropertyName("locality")] string? Locality,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("village")] string? Village,
        [property: JsonPropertyName("municipality")] string? Municipality,
        [property: JsonPropertyName("county")] string? County,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("province")] string? Province,
        [property: JsonPropertyName("region")] string? Region,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("postcode")] string? Postcode,
        [property: JsonPropertyName("amenity")] string? Amenity,
        [property: JsonPropertyName("shop")] string? Shop,
        [property: JsonPropertyName("building")] string? Building,
        [property: JsonPropertyName("tourism")] string? Tourism,
        [property: JsonPropertyName("office")] string? Office,
        [property: JsonPropertyName("leisure")] string? Leisure,
        [property: JsonPropertyName("craft")] string? Craft,
        [property: JsonPropertyName("historic")] string? Historic);

    private sealed record GoogleNearbyResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("results")] List<GoogleNearbyResult>? Results);

    private sealed record GoogleNearbyResult(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("types")] List<string>? Types,
        [property: JsonPropertyName("vicinity")] string? Vicinity);

    private sealed record GoogleGeocodeResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("results")] List<GoogleGeocodeResult>? Results);

    private sealed record GoogleGeocodeResult(
        [property: JsonPropertyName("formatted_address")] string? FormattedAddress,
        [property: JsonPropertyName("types")] List<string>? Types,
        [property: JsonPropertyName("address_components")] List<GoogleAddressComponent>? AddressComponents);

    private sealed record GoogleAddressComponent(
        [property: JsonPropertyName("long_name")] string? LongName,
        [property: JsonPropertyName("short_name")] string? ShortName,
        [property: JsonPropertyName("types")] List<string>? Types);
}
