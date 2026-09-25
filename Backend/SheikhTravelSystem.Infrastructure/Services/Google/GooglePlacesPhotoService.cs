using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Google;

/// <summary>Places API (New) Place Photos media resolver for Live Map detail cards.</summary>
public sealed class GooglePlacesPhotoService(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleMapsOptions> options,
    IMemoryCache memoryCache,
    ILogger<GooglePlacesPhotoService> logger) : IGooglePlacesPhotoService
{
    public async Task<NearbyPlacePhotoDto?> ResolveMediaAsync(
        string photoResourceName,
        int maxWidthPx = 800,
        CancellationToken cancellationToken = default)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null)
            return null;

        if (!TryNormalizePhotoResourceName(photoResourceName, out var resourceName))
            return null;

        maxWidthPx = Math.Clamp(maxWidthPx, 100, 1600);
        var cacheKey = GoogleMapsApiHelper.BuildCacheKey(
            "place-photo",
            resourceName,
            maxWidthPx.ToString(CultureInfo.InvariantCulture));

        if (memoryCache.TryGetValue(cacheKey, out NearbyPlacePhotoDto? cached) && cached is not null)
            return cached;

        try
        {
            var client = httpClientFactory.CreateClient("GooglePlaces");
            var path =
                $"v1/{resourceName}/media?maxWidthPx={maxWidthPx.ToString(CultureInfo.InvariantCulture)}"
                + "&skipHttpRedirect=true";

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Goog-Api-Key", credential);

            var referer = options.Value.ServerHttpReferer?.Trim();
            if (!string.IsNullOrEmpty(referer)
                && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                request.Headers.Referrer = refererUri;
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "(none)";
            var status = (int)response.StatusCode;
            var bodyPreview = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncated = bodyPreview.Length > 500 ? bodyPreview[..500] : bodyPreview;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Places Photo media failed. StatusCode={StatusCode} ContentType={ContentType} Resource={Resource} Body={Body}",
                    status,
                    contentType,
                    resourceName,
                    truncated);
                return null;
            }

            // Binary image response (skipHttpRedirect ignored / bytes returned)
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;

            PhotoMediaResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<PhotoMediaResponse>(bodyPreview);
            }
            catch (Exception)
            {
                return null;
            }

            var uri = payload?.PhotoUri?.Trim();
            if (string.IsNullOrWhiteSpace(uri))
                return null;

            var result = new NearbyPlacePhotoDto(uri, Array.Empty<string>());
            var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
            memoryCache.Set(cacheKey, result, ttl);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Places Photo media exception. Resource={Resource}", resourceName);
            return null;
        }
    }

    /// <summary>
    /// Accepts <c>places/.../photos/...</c> only (optionally with a leading <c>v1/</c>).
    /// Rejects absolute URLs and path traversal.
    /// </summary>
    internal static bool TryNormalizePhotoResourceName(string? raw, out string resourceName)
    {
        resourceName = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var name = raw.Trim();
        if (name.StartsWith("v1/", StringComparison.OrdinalIgnoreCase))
            name = name[3..];

        if (name.Contains("://", StringComparison.Ordinal)
            || name.Contains("..", StringComparison.Ordinal)
            || name.Contains('\\', StringComparison.Ordinal)
            || name.StartsWith('/'))
        {
            return false;
        }

        if (!name.StartsWith("places/", StringComparison.OrdinalIgnoreCase)
            || !name.Contains("/photos/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resourceName = name;
        return true;
    }

    private sealed class PhotoMediaResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("photoUri")]
        public string? PhotoUri { get; set; }
    }
}
