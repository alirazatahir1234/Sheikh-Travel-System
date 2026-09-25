using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Google;

/// <summary>Places API (New) <c>places:searchNearby</c> for Live Map nearby amenities.</summary>
public sealed class GooglePlacesNearbyService(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleMapsOptions> options,
    IMemoryCache memoryCache,
    IGooglePlacesPhotoService photoService,
    ILogger<GooglePlacesNearbyService> logger) : IGooglePlacesNearbyService
{
    private const string FieldMask =
        "places.id,places.displayName,places.formattedAddress,places.location,"
        + "places.types,places.rating,places.userRatingCount,places.currentOpeningHours,places.googleMapsUri,"
        + "places.photos";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Dictionary<string, string[]> CategoryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fuel"] = ["gas_station"],
        ["workshop"] = ["car_repair"],
        ["hospital"] = ["hospital"],
        ["restaurant"] = ["restaurant"],
        ["hotel"] = ["lodging"],
        ["parking"] = ["parking"],
        ["atm"] = ["atm"],
        ["airport"] = ["airport"],
        // Curated amenity set for "More" — real Places types only (no fake data).
        ["more"] = ["convenience_store", "shopping_mall", "pharmacy"]
    };

    public static IReadOnlyCollection<string> SupportedCategories { get; } = CategoryTypes.Keys.ToList();

    public async Task<NearbyPlacesSearchResult> SearchNearbyAsync(
        double latitude,
        double longitude,
        string category,
        int radiusMeters = 1500,
        int maxResults = 8,
        CancellationToken cancellationToken = default)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null)
        {
            return NearbyPlacesSearchResult.Fail(
                "Nearby places are unavailable. Set GoogleMaps server credential (SheikhGo-Backend) via user secrets or env.",
                NearbyPlacesErrorCodes.Configuration);
        }

        if (!CategoryTypes.TryGetValue(category.Trim(), out var includedTypes))
            return NearbyPlacesSearchResult.Ok(Array.Empty<NearbyPlaceDto>());

        radiusMeters = Math.Clamp(radiusMeters, 100, 5000);
        maxResults = Math.Clamp(maxResults, 1, 20);
        var categoryKey = category.Trim().ToLowerInvariant();

        var cacheKey = GoogleMapsApiHelper.BuildCacheKey(
            "places-nearby",
            GoogleMapsApiHelper.FormatCoord(latitude),
            GoogleMapsApiHelper.FormatCoord(longitude),
            categoryKey,
            radiusMeters.ToString(CultureInfo.InvariantCulture),
            maxResults.ToString(CultureInfo.InvariantCulture));

        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyList<NearbyPlaceDto>? cached) && cached is not null)
            return NearbyPlacesSearchResult.Ok(cached);

        try
        {
            var body = new NearbySearchRequest
            {
                IncludedTypes = includedTypes,
                MaxResultCount = maxResults,
                RankPreference = "DISTANCE",
                LanguageCode = "en",
                LocationRestriction = new LocationRestriction
                {
                    Circle = new CircleRestriction
                    {
                        Center = new LatLngLiteral { Latitude = latitude, Longitude = longitude },
                        Radius = radiusMeters
                    }
                }
            };

            var client = httpClientFactory.CreateClient("GooglePlaces");
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchNearby")
            {
                Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Goog-Api-Key", credential);
            request.Headers.Add("X-Goog-FieldMask", FieldMask);
            // Website-restricted keys reject empty Referer; send an allow-listed origin when configured.
            var referer = options.Value.ServerHttpReferer?.Trim();
            if (!string.IsNullOrEmpty(referer)
                && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                request.Headers.Referrer = refererUri;
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                var truncated = err.Length > 400 ? err[..400] : err;
                var (message, errorCode, reasonTag) = MapGoogleError(err, response.StatusCode);

                logger.LogWarning(
                    "Places Nearby Search failed. Category={Category} Lat={Lat} Lng={Lng} RadiusMeters={Radius} MaxResults={MaxResults} StatusCode={StatusCode} Reason={Reason} Body={Body}",
                    categoryKey,
                    GoogleMapsApiHelper.FormatCoord(latitude),
                    GoogleMapsApiHelper.FormatCoord(longitude),
                    radiusMeters,
                    maxResults,
                    (int)response.StatusCode,
                    reasonTag,
                    truncated);

                return NearbyPlacesSearchResult.Fail(message, errorCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<NearbySearchResponse>(cancellationToken);
            var places = payload?.Places ?? [];
            var results = places
                .Select(p => MapPlace(p, latitude, longitude, categoryKey))
                .Where(p => p is not null)
                .Cast<NearbyPlaceDto>()
                .OrderBy(p => p.DistanceMeters ?? double.MaxValue)
                .ToList();

            results = await EnrichWithPhotoUrlsAsync(results, cancellationToken);

            var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
            memoryCache.Set(cacheKey, (IReadOnlyList<NearbyPlaceDto>)results, ttl);
            return NearbyPlacesSearchResult.Ok(results);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Places Nearby Search exception. Category={Category} Lat={Lat} Lng={Lng} RadiusMeters={Radius}",
                categoryKey,
                GoogleMapsApiHelper.FormatCoord(latitude),
                GoogleMapsApiHelper.FormatCoord(longitude),
                radiusMeters);
            return NearbyPlacesSearchResult.Fail(
                "Nearby Search is temporarily unavailable. Places API (New) request failed on the backend.",
                NearbyPlacesErrorCodes.GoogleApi);
        }
    }

    /// <summary>
    /// Resolves Place Photos media URLs for places that have a photo resource.
    /// Soft-fails per place — never fails the Nearby Search batch.
    /// </summary>
    private async Task<List<NearbyPlaceDto>> EnrichWithPhotoUrlsAsync(
        List<NearbyPlaceDto> places,
        CancellationToken cancellationToken)
    {
        if (places.Count == 0) return places;

        const int maxConcurrency = 3;
        using var gate = new SemaphoreSlim(maxConcurrency);
        var tasks = places.Select(async place =>
        {
            if (string.IsNullOrWhiteSpace(place.PhotoResourceName))
                return place;

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var media = await photoService.ResolveMediaAsync(
                    place.PhotoResourceName,
                    maxWidthPx: 800,
                    cancellationToken).ConfigureAwait(false);

                return media?.PhotoUrl is not null
                    ? place with { PhotoUrl = media.PhotoUrl }
                    : place;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Photo enrich soft-fail for {Resource}", place.PhotoResourceName);
                return place;
            }
            finally
            {
                gate.Release();
            }
        });

        var enriched = await Task.WhenAll(tasks).ConfigureAwait(false);
        return enriched.ToList();
    }

    private static (string Message, string ErrorCode, string ReasonTag) MapGoogleError(
        string body,
        System.Net.HttpStatusCode statusCode)
    {
        if (body.Contains("API_KEY_HTTP_REFERRER_BLOCKED", StringComparison.Ordinal)
            || body.Contains("Requests from referer", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "Nearby places are unavailable. SheikhGo-Backend key uses Website (HTTP referrer) restrictions — "
                + "in Cloud Console set Application restrictions to None or IP addresses, "
                + "or set GoogleMaps:ServerHttpReferer to an allow-listed URL (e.g. http://127.0.0.1:5082/).",
                NearbyPlacesErrorCodes.Authentication,
                "REFERRER_BLOCKED");
        }

        if (body.Contains("SERVICE_DISABLED", StringComparison.Ordinal)
            || body.Contains("has not been used", StringComparison.OrdinalIgnoreCase)
            || body.Contains("is disabled", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "Nearby places are unavailable. Enable Places API (New) for the backend Google Cloud project.",
                NearbyPlacesErrorCodes.Configuration,
                "SERVICE_DISABLED");
        }

        if (statusCode == System.Net.HttpStatusCode.Forbidden
            || statusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return (
                "Nearby places are unavailable. Check Places API (New) and backend key restrictions (IP, not HTTP referrer).",
                NearbyPlacesErrorCodes.Authentication,
                "AUTH");
        }

        if (statusCode == System.Net.HttpStatusCode.BadRequest)
        {
            return (
                "Nearby Search request was invalid. Check coordinates, category, and radius.",
                NearbyPlacesErrorCodes.InvalidRequest,
                "INVALID_REQUEST");
        }

        return (
            "Nearby Search is temporarily unavailable. Places API (New) returned an error from Google.",
            NearbyPlacesErrorCodes.GoogleApi,
            "OTHER");
    }

    private static NearbyPlaceDto? MapPlace(
        NearbyPlaceJson place,
        double originLat,
        double originLng,
        string category)
    {
        var lat = place.Location?.Latitude;
        var lng = place.Location?.Longitude;
        if (lat is null || lng is null) return null;

        var name = place.DisplayName?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;

        var id = place.Id?.Trim() ?? $"{lat},{lng}";
        var distance = HaversineMeters(originLat, originLng, lat.Value, lng.Value);
        var (openNow, openingStatus) = MapOpeningHours(place.CurrentOpeningHours);
        var (photoResourceName, photoAttributions) = MapFirstPhoto(place.Photos);

        return new NearbyPlaceDto(
            id,
            name,
            place.FormattedAddress,
            lat.Value,
            lng.Value,
            Math.Round(distance, 1),
            place.Rating,
            place.UserRatingCount,
            place.Types ?? [],
            category,
            openNow,
            openingStatus,
            string.IsNullOrWhiteSpace(place.GoogleMapsUri) ? null : place.GoogleMapsUri.Trim(),
            photoResourceName,
            photoAttributions,
            PhotoUrl: null);
    }

    private static (string? ResourceName, IReadOnlyList<string>? Attributions) MapFirstPhoto(
        List<PlacePhotoJson>? photos)
    {
        var first = photos?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Name));
        if (first is null) return (null, null);

        var name = first.Name!.Trim();
        var attributions = first.AuthorAttributions?
            .Select(a =>
            {
                var display = a.DisplayName?.Trim();
                if (!string.IsNullOrWhiteSpace(display)) return display;
                var uri = a.Uri?.Trim();
                return string.IsNullOrWhiteSpace(uri) ? null : uri;
            })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (name, attributions is { Count: > 0 } ? attributions : null);
    }

    private static (bool? OpenNow, string? Status) MapOpeningHours(OpeningHoursJson? hours)
    {
        if (hours?.OpenNow is null) return (null, null);
        var open = hours.OpenNow.Value;
        if (!open) return (false, "Closed");

        // Prefer a human period line when Google returns weekdayDescriptions (e.g. "Monday: 8 AM – 11 PM").
        var todayHint = hours.WeekdayDescriptions?
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        if (!string.IsNullOrWhiteSpace(todayHint) && todayHint.Contains('–', StringComparison.Ordinal))
        {
            var parts = todayHint.Split('–', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                return (true, $"Open · Closes {parts[1].Trim()}");
        }

        return (true, "Open");
    }

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        static double Rad(double d) => d * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private sealed class NearbySearchRequest
    {
        public string[] IncludedTypes { get; set; } = [];
        public int MaxResultCount { get; set; }
        public string RankPreference { get; set; } = "DISTANCE";
        public string LanguageCode { get; set; } = "en";
        public LocationRestriction LocationRestriction { get; set; } = new();
    }

    private sealed class LocationRestriction
    {
        public CircleRestriction Circle { get; set; } = new();
    }

    private sealed class CircleRestriction
    {
        public LatLngLiteral Center { get; set; } = new();
        public double Radius { get; set; }
    }

    private sealed class LatLngLiteral
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class NearbySearchResponse
    {
        public List<NearbyPlaceJson>? Places { get; set; }
    }

    private sealed class NearbyPlaceJson
    {
        public string? Id { get; set; }
        public DisplayNameJson? DisplayName { get; set; }
        public string? FormattedAddress { get; set; }
        public LatLngLiteral? Location { get; set; }
        public List<string>? Types { get; set; }
        public double? Rating { get; set; }
        public int? UserRatingCount { get; set; }
        public OpeningHoursJson? CurrentOpeningHours { get; set; }
        public string? GoogleMapsUri { get; set; }
        public List<PlacePhotoJson>? Photos { get; set; }
    }

    private sealed class PlacePhotoJson
    {
        public string? Name { get; set; }
        public List<AuthorAttributionJson>? AuthorAttributions { get; set; }
    }

    private sealed class AuthorAttributionJson
    {
        public string? DisplayName { get; set; }
        public string? Uri { get; set; }
        public string? PhotoUri { get; set; }
    }

    private sealed class OpeningHoursJson
    {
        public bool? OpenNow { get; set; }
        public List<string>? WeekdayDescriptions { get; set; }
    }

    private sealed class DisplayNameJson
    {
        public string? Text { get; set; }
    }
}
