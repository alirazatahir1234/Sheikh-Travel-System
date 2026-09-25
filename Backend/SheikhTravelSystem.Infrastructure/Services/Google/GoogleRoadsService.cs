using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services.Google;

public sealed class GoogleRoadsService(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleRoadsService> logger) : IGoogleRoadsService
{
    private const int MaxPointsPerRequest = 100;

    public async Task<IReadOnlyList<SnappedGpsPoint>?> SnapToRoadsAsync(
        IReadOnlyList<LatLngPoint> points,
        CancellationToken cancellationToken = default)
    {
        var credential = GoogleMapsApiHelper.ResolveServerKey(options);
        if (credential is null || points.Count == 0) return null;

        if (points.Count < options.Value.RoadsMinPoints)
            return null;

        try
        {
            var client = httpClientFactory.CreateClient("GoogleMaps");
            var snapped = new List<SnappedGpsPoint>();

            for (var offset = 0; offset < points.Count; offset += MaxPointsPerRequest)
            {
                var batch = points.Skip(offset).Take(MaxPointsPerRequest).ToList();
                var path = string.Join('|', batch.Select(p =>
                    $"{GoogleMapsApiHelper.FormatCoord(p.Latitude)},{GoogleMapsApiHelper.FormatCoord(p.Longitude)}"));

                var url = $"/maps/api/roads/v1/snapToRoads?path={Uri.EscapeDataString(path)}&interpolate=true&key={Uri.EscapeDataString(credential)}";
                var response = await client.GetFromJsonAsync<SnapToRoadsResponse>(url, cancellationToken);
                if (response?.SnappedPoints is not { Count: > 0 })
                    continue;

                snapped.AddRange(response.SnappedPoints.Select(sp => new SnappedGpsPoint(
                    sp.Location?.Latitude ?? 0,
                    sp.Location?.Longitude ?? 0,
                    sp.OriginalIndex)));
            }

            return snapped.Count > 0 ? snapped : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Google Roads snapToRoads failed");
            return null;
        }
    }

    private sealed class SnapToRoadsResponse
    {
        [JsonPropertyName("snappedPoints")]
        public List<SnappedPointJson>? SnappedPoints { get; set; }
    }

    private sealed class SnappedPointJson
    {
        [JsonPropertyName("location")]
        public SnapLocationJson? Location { get; set; }

        [JsonPropertyName("originalIndex")]
        public int? OriginalIndex { get; set; }
    }

    private sealed class SnapLocationJson
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }
}
