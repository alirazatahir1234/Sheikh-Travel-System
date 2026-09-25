using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Google;

public sealed class GoogleTimeZoneService(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleMapsOptions> options,
    IMemoryCache memoryCache,
    ILogger<GoogleTimeZoneService> logger) : IGoogleTimeZoneService
{
    public async Task<GoogleTimeZoneResult?> GetTimeZoneAsync(
        double latitude,
        double longitude,
        DateTime? timestampUtc = null,
        CancellationToken cancellationToken = default)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null) return null;

        var ts = timestampUtc ?? DateTime.UtcNow;
        var unix = new DateTimeOffset(ts.ToUniversalTime()).ToUnixTimeSeconds();

        var cacheKey = GoogleMapsApiHelper.BuildCacheKey(
            "timezone",
            GoogleMapsApiHelper.FormatCoord(latitude),
            GoogleMapsApiHelper.FormatCoord(longitude),
            unix.ToString(CultureInfo.InvariantCulture));

        if (memoryCache.TryGetValue(cacheKey, out GoogleTimeZoneResult? cached))
            return cached;

        try
        {
            var client = httpClientFactory.CreateClient("GoogleMaps");
            var lat = GoogleMapsApiHelper.FormatCoord(latitude);
            var lng = GoogleMapsApiHelper.FormatCoord(longitude);
            var url = $"/maps/api/timezone/json?location={lat},{lng}&timestamp={unix}&key={Uri.EscapeDataString(credential)}";

            var response = await client.GetFromJsonAsync<TimeZoneApiResponse>(url, cancellationToken);
            if (response?.Status != "OK" || string.IsNullOrWhiteSpace(response.TimeZoneId))
                return null;

            var result = new GoogleTimeZoneResult(
                response.TimeZoneId,
                response.RawOffset,
                response.DstOffset);

            var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
            memoryCache.Set(cacheKey, result, ttl);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Google Time Zone API call failed");
            return null;
        }
    }

    private sealed class TimeZoneApiResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("timeZoneId")]
        public string? TimeZoneId { get; set; }

        [JsonPropertyName("rawOffset")]
        public int RawOffset { get; set; }

        [JsonPropertyName("dstOffset")]
        public int DstOffset { get; set; }
    }
}
