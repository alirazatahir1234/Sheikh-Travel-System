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
    /// <summary>Only keep Nearby POIs this close to the GPS fix (meters).</summary>
    private const double MaxPlaceDistanceMeters = 40;
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
            try
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
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to read GpsAddressCache for {Lat},{Lng}; continuing with providers",
                    latitude,
                    longitude);
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

        // Persist street-first lines only (no legacy Near-prefix / plus-code fragments).
        var cleanedAddress = StripPlusCodeSegments(resolved.FormattedAddress);
        if (cleanedAddress.StartsWith("Near ", StringComparison.OrdinalIgnoreCase))
        {
            var comma = cleanedAddress.IndexOf(',');
            cleanedAddress = comma > 0 ? cleanedAddress[(comma + 1)..].Trim() : cleanedAddress;
        }
        cleanedAddress = CompactLocalityAddress(
            cleanedAddress,
            resolved.Road,
            resolved.City,
            resolved.State,
            resolved.Country);
        if (string.IsNullOrWhiteSpace(cleanedAddress))
            cleanedAddress = resolved.FormattedAddress;

        resolved = resolved with
        {
            FormattedAddress = cleanedAddress,
            // Fleet operators want road/city — Nearby Places names are often wrong or garbled.
            PlaceName = null,
            PlaceType = null
        };

        try
        {
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
                    PlaceType = resolved.PlaceName is null ? null : resolved.PlaceType,
                    ResolvedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            // Never fail the client response because cache write failed (schema drift, etc.).
            logger.LogWarning(
                ex,
                "Failed to cache reverse-geocode result for {Lat},{Lng}; returning resolved address anyway",
                latitude,
                longitude);
        }

        return resolved with { FromCache = false };
    }

    /// <summary>
    /// Missing street, plus-code-only, or legacy "Near {POI}" lines. PlaceName does not make an address fine.
    /// </summary>
    internal static bool IsCoarseAddress(string? address, string? road, string? placeName = null)
    {
        _ = placeName; // ignored for coarseness (street-first policy)
        if (!string.IsNullOrWhiteSpace(road)
            && !LooksLikeAdminToken(road)
            && !LooksLikePlusCode(road))
            return false;

        if (string.IsNullOrWhiteSpace(address)) return true;

        var trimmed = address.Trim();

        // Legacy "Near {POI}, …" — always refresh regardless of digits / plus-codes in the rest.
        if (trimmed.StartsWith("Near ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (ContainsPlusCode(trimmed))
            return true;

        var parts = trimmed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        // Digits usually mean house number / road code — but not when the only "digit token" was a plus code
        // (already handled above).
        var hasDigit = parts.Any(p => p.Any(char.IsDigit) && !LooksLikePlusCode(p));
        if (hasDigit) return false;

        // Explicit admin hierarchy tokens mean we still want a street if providers can supply one.
        var lowerAll = trimmed.ToLowerInvariant();
        if (lowerAll.Contains("tehsil") || lowerAll.Contains("district") || lowerAll.Contains("division"))
            return true;

        // "City, Province, Country" with no road is acceptable for rural pins — do not keep thrashing providers.
        return false;
    }

    internal static bool ContainsPlusCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        // Google Open Location Code fragments: "7M78+84W" or full "7M78+84W Pasrur"
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"\b[23456789CFGHJMPQRVWX]{4,8}\+[23456789CFGHJMPQRVWX]{2,3}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool LooksLikePlusCode(string part) => ContainsPlusCode(part);

    private static bool LooksLikeAdminToken(string part)
    {
        var lower = part.Trim().ToLowerInvariant();
        if (AdminOnlyTokens.Contains(lower)) return true;
        return AdminOnlyTokens.Any(t => lower.Contains(t, StringComparison.Ordinal));
    }

    private static ReverseGeocodeResult MergeResults(ReverseGeocodeResult? preferred, ReverseGeocodeResult fallback)
    {
        if (preferred is null) return fallback;

        var preferredFine = !IsCoarseAddress(preferred.FormattedAddress, preferred.Road);

        // Prefer Nominatim street when Google only gave plus-code / coarse locality.
        var address = preferred.FormattedAddress;
        var road = FirstNonEmpty(preferred.Road, fallback.Road);
        if (IsCoarseAddress(address, preferred.Road)
            && !IsCoarseAddress(fallback.FormattedAddress, fallback.Road))
        {
            address = fallback.FormattedAddress;
            road = FirstNonEmpty(fallback.Road, road);
            preferredFine = false;
        }

        // Never promote Nearby POI into fleet address; keep PlaceName only when street is already fine
        // and the POI survived sanitisation.
        var place = preferredFine ? FirstNonEmpty(preferred.PlaceName) : null;
        var placeType = place is null ? null : preferred.PlaceType;
        var city = FirstNonEmpty(
            preferredFine ? preferred.City : null,
            fallback.City,
            preferred.City);
        var state = FirstNonEmpty(preferred.State, fallback.State);
        var country = FirstNonEmpty(preferred.Country, fallback.Country);
        var postal = FirstNonEmpty(preferred.PostalCode, fallback.PostalCode);

        address = StripPlusCodeSegments(address ?? string.Empty);
        if (string.IsNullOrWhiteSpace(address))
            address = fallback.FormattedAddress;

        return new ReverseGeocodeResult(address!, road, city, state, country, postal, false, place, placeType);
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

            // radius=100 + geometry; we filter by Haversine ≤40m (prominence ranking alone is unsafe).
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
            // Nearby POIs are often wrong/garbled for fleet ops — keep distance-qualified name as
            // optional metadata only; never fold into FormattedAddress.
            if (nearby?.Status == "OK" && nearby.Results is { Count: > 0 })
            {
                var bestPlace = PickClosestNearbyPlace(nearby.Results, latitude, longitude);
                if (bestPlace is not null)
                {
                    placeName = SanitizePlaceName(bestPlace.Name!.Trim());
                    var types = bestPlace.Types ?? [];
                    placeType = placeName is null
                        ? null
                        : types.FirstOrDefault(t => !IgnoredPlaceTypes.Contains(t)) ?? types.FirstOrDefault();
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
                    }
                }
            }

            // Prefer a rebuilt street line over plus-codes or empty route results.
            var rebuilt = string.Join(", ", new[] { road, city, state, country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(formatted))
                formatted = rebuilt;
            else if (ContainsPlusCode(formatted) && !string.IsNullOrWhiteSpace(rebuilt))
                formatted = rebuilt;
            else
                formatted = StripPlusCodeSegments(formatted);

            if (string.IsNullOrWhiteSpace(formatted) && string.IsNullOrWhiteSpace(placeName))
                return null;

            formatted = string.IsNullOrWhiteSpace(formatted) ? rebuilt : formatted;
            if (string.IsNullOrWhiteSpace(formatted))
                return null;

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

    /// <summary>
    /// Closest non-admin Nearby result within <see cref="MaxPlaceDistanceMeters"/>; otherwise null.
    /// </summary>
    private static GoogleNearbyResult? PickClosestNearbyPlace(
        IReadOnlyList<GoogleNearbyResult> results,
        double originLat,
        double originLng)
    {
        GoogleNearbyResult? best = null;
        var bestMeters = double.MaxValue;

        foreach (var place in results)
        {
            if (string.IsNullOrWhiteSpace(place.Name)) continue;
            var types = place.Types ?? [];
            if (types.Any(t => IgnoredPlaceTypes.Contains(t))) continue;
            if (types.Contains("locality", StringComparer.OrdinalIgnoreCase)) continue;

            var loc = place.Geometry?.Location;
            if (loc is null) continue;

            var meters = HaversineMeters(originLat, originLng, loc.Lat, loc.Lng);
            if (meters > MaxPlaceDistanceMeters) continue;
            if (meters < bestMeters)
            {
                bestMeters = meters;
                best = place;
            }
        }

        return best;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static GoogleGeocodeResult? PickBestGeocodeResult(IReadOnlyList<GoogleGeocodeResult> results)
    {
        static int Score(GoogleGeocodeResult r)
        {
            var types = r.Types ?? [];
            if (types.Contains("street_address")) return 100;
            if (types.Contains("premise")) return 90;
            if (types.Contains("route")) return 80;
            if (types.Contains("neighborhood") || types.Contains("sublocality")) return 60;
            if (types.Contains("establishment") || types.Contains("point_of_interest")) return 40;
            if (types.Contains("plus_code")) return 5;
            return 30;
        }

        return results.OrderByDescending(Score).FirstOrDefault();
    }

    /// <summary>Drop obvious OCR/garbage place labels (e.g. "evcuu trsut").</summary>
    private static string? SanitizePlaceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        if (trimmed.Length < 3) return null;
        // Very short token noise / repeated nonsense letters common in bad Places listings.
        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Count(t => t.Length >= 4 && !t.Any(char.IsDigit)
                && t.Distinct().Count() <= 2) >= 1)
            return null;
        if (tokens.Any(t => LooksLikeTypoBlob(t)))
            return null;
        return trimmed;
    }

    private static bool LooksLikeTypoBlob(string token)
    {
        if (token.Length < 4) return false;
        // "evcuu", "trsut" style: unusual vowel clusters without dictionary letters pattern —
        // flag tokens with 3+ consecutive identical letters or no vowels.
        if (System.Text.RegularExpressions.Regex.IsMatch(token, @"(.)\1{2,}")) return true;
        var vowels = token.Count(c => "aeiouAEIOU".Contains(c));
        return vowels == 0 && token.All(char.IsLetter);
    }

    private static string StripPlusCodeSegments(string address)
    {
        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !LooksLikePlusCode(p))
            .ToList();
        return parts.Count == 0 ? address.Trim() : string.Join(", ", parts);
    }

    /// <summary>
    /// When there is no usable road, prefer "City, State, Country" over long tehsil/district chains.
    /// </summary>
    private static string CompactLocalityAddress(
        string address,
        string? road,
        string? city,
        string? state,
        string? country)
    {
        if (!string.IsNullOrWhiteSpace(road) && !LooksLikeAdminToken(road) && !LooksLikePlusCode(road))
            return address;

        var compact = string.Join(", ", new[] { city, state, country }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(compact))
            return compact;

        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !LooksLikeAdminToken(p) || AdminOnlyTokens.Contains(p.Trim().ToLowerInvariant()))
            .Where(p =>
            {
                var lower = p.ToLowerInvariant();
                return !lower.Contains("tehsil")
                    && !lower.Contains("district")
                    && !lower.Contains("division");
            })
            .Where(p => !LooksLikePlusCode(p) && !p.All(char.IsDigit))
            .ToList();

        return parts.Count > 0 ? string.Join(", ", parts) : address;
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
        [property: JsonPropertyName("vicinity")] string? Vicinity,
        [property: JsonPropertyName("geometry")] GoogleGeometry? Geometry);

    private sealed record GoogleGeometry(
        [property: JsonPropertyName("location")] GoogleLatLng? Location);

    private sealed record GoogleLatLng(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lng")] double Lng);

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
