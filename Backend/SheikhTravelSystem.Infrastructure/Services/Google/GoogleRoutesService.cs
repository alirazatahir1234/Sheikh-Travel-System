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

public sealed class GoogleRoutesService(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleMapsOptions> options,
    IMemoryCache memoryCache,
    ILogger<GoogleRoutesService> logger) : IGoogleRoutesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<GoogleRouteResult?> ComputeRouteAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        IReadOnlyList<LatLngPoint>? waypoints = null,
        CancellationToken cancellationToken = default)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null) return null;

        var waypointKey = waypoints is { Count: > 0 }
            ? string.Join(';', waypoints.Select(p =>
                $"{GoogleMapsApiHelper.FormatCoord(p.Latitude)},{GoogleMapsApiHelper.FormatCoord(p.Longitude)}"))
            : string.Empty;

        var cacheKey = GoogleMapsApiHelper.BuildCacheKey(
            "routes",
            GoogleMapsApiHelper.FormatCoord(originLatitude),
            GoogleMapsApiHelper.FormatCoord(originLongitude),
            GoogleMapsApiHelper.FormatCoord(destinationLatitude),
            GoogleMapsApiHelper.FormatCoord(destinationLongitude),
            waypointKey);

        if (memoryCache.TryGetValue(cacheKey, out GoogleRouteResult? cached))
            return cached;

        try
        {
            var body = new RoutesComputeRequest
            {
                Origin = LatLngWaypoint(originLatitude, originLongitude),
                Destination = LatLngWaypoint(destinationLatitude, destinationLongitude),
                TravelMode = "DRIVE",
                RoutingPreference = "TRAFFIC_AWARE",
                Intermediates = waypoints?.Select(w => LatLngWaypoint(w.Latitude, w.Longitude)).ToList()
            };

            var client = httpClientFactory.CreateClient("GoogleRoutes");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/directions/v2:computeRoutes")
            {
                Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Goog-Api-Key", credential);
            request.Headers.Add("X-Goog-FieldMask", "routes.duration,routes.distanceMeters,routes.polyline.encodedPolyline");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google Routes API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<RoutesComputeResponse>(cancellationToken);
            var route = payload?.Routes?.FirstOrDefault();
            if (route is null) return null;

            var durationSeconds = ParseDurationSeconds(route.Duration);
            var result = new GoogleRouteResult(
                route.DistanceMeters ?? 0,
                durationSeconds,
                route.Polyline?.EncodedPolyline,
                null);

            var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
            memoryCache.Set(cacheKey, result, ttl);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Google Routes API call failed");
            return null;
        }
    }

    private static RoutesWaypoint LatLngWaypoint(double lat, double lng) => new()
    {
        Location = new RoutesLocation
        {
            LatLng = new RoutesLatLng
            {
                Latitude = lat,
                Longitude = lng
            }
        }
    };

    private static int ParseDurationSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return 0;
        if (duration.EndsWith("s", StringComparison.Ordinal)
            && double.TryParse(duration.AsSpan(0, duration.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return (int)Math.Ceiling(seconds);
        }

        return 0;
    }

    private sealed class RoutesComputeRequest
    {
        public RoutesWaypoint Origin { get; set; } = null!;
        public RoutesWaypoint Destination { get; set; } = null!;
        public string TravelMode { get; set; } = "DRIVE";
        public string RoutingPreference { get; set; } = "TRAFFIC_AWARE";
        public List<RoutesWaypoint>? Intermediates { get; set; }
    }

    private sealed class RoutesWaypoint
    {
        public RoutesLocation Location { get; set; } = null!;
    }

    private sealed class RoutesLocation
    {
        public RoutesLatLng LatLng { get; set; } = null!;
    }

    private sealed class RoutesLatLng
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class RoutesComputeResponse
    {
        public List<RoutesRoute>? Routes { get; set; }
    }

    private sealed class RoutesRoute
    {
        public string? Duration { get; set; }
        public int? DistanceMeters { get; set; }
        public RoutesPolyline? Polyline { get; set; }
    }

    private sealed class RoutesPolyline
    {
        public string? EncodedPolyline { get; set; }
    }
}
