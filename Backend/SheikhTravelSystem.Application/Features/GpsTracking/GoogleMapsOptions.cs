using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace SheikhTravelSystem.Application.Features.GpsTracking;

/// <summary>
/// Shared SheikhGo-Backend Google Maps Platform options.
/// Prefer env <c>GoogleMaps__ServerKey</c> (or legacy <c>GoogleMaps__ApiKey</c> via
/// <see cref="ConfigurationKeyNameAttribute"/>) or reuse Geocoding fallback.
/// Never expose this credential to browser / Flutter clients.
/// </summary>
public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// IP-restricted server credential (SheikhGo-Backend).
    /// Config key remains <c>ApiKey</c> for existing env/user-secrets compatibility.
    /// </summary>
    [ConfigurationKeyName("ApiKey")]
    [JsonPropertyName("apiKey")]
    public string? ServerKey { get; set; }

    /// <summary>
    /// Optional <c>Referer</c> sent on server-side Places/Maps calls when the backend key is
    /// still restricted by <b>Websites</b> (HTTP referrers) in Cloud Console.
    /// Prefer changing Application restrictions to <b>None</b> or <b>IP addresses</b> instead.
    /// Example: <c>http://127.0.0.1:5082/</c> matching an allowed website pattern.
    /// </summary>
    public string? ServerHttpReferer { get; set; }

    /// <summary>Cache TTL for Routes / Time Zone responses (minutes).</summary>
    public int CacheMinutes { get; set; } = 15;

    /// <summary>Minimum GPS points before Roads snapToRoads is attempted.</summary>
    public int RoadsMinPoints { get; set; } = 5;
}
