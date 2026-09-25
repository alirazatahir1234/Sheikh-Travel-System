using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Google Maps Platform proxies under <c>api/gps/maps/*</c>.
/// Kept as a dedicated controller so Maps changes do not inflate GpsTrackingController blast radius.
/// </summary>
[Authorize]
[RequirePermission(AnalyticsPermissions.GpsView)]
[Route("api/gps/maps")]
public sealed class GoogleMapsController : BaseApiController
{
    [HttpGet("planned-route")]
    public async Task<IActionResult> GetPlannedRoute(
        [FromQuery] double originLat,
        [FromQuery] double originLng,
        [FromQuery] double destLat,
        [FromQuery] double destLng,
        [FromQuery] string? waypoints = null)
    {
        IReadOnlyList<LatLngPoint>? parsedWaypoints = null;
        if (!string.IsNullOrWhiteSpace(waypoints))
        {
            parsedWaypoints = waypoints
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseLatLng)
                .OfType<LatLngPoint>()
                .ToList();
        }

        return Ok(await Mediator.Send(new GetPlannedRouteQuery(
            originLat, originLng, destLat, destLng, parsedWaypoints)));
    }

    [HttpGet("timezone")]
    public async Task<IActionResult> GetLocationTimeZone(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] DateTime? timestampUtc = null)
        => Ok(await Mediator.Send(new GetLocationTimeZoneQuery(lat, lng, timestampUtc)));

    [HttpPost("snap-path")]
    public async Task<IActionResult> SnapGpsPath([FromBody] IReadOnlyList<LatLngPoint> points)
        => Ok(await Mediator.Send(new SnapGpsPathQuery(points)));

    [HttpPost("optimize-tours")]
    public async Task<IActionResult> OptimizeDispatchTours([FromBody] OptimizeDispatchToursRequestDto body)
        => Ok(await Mediator.Send(new OptimizeDispatchToursCommand(body)));

    [HttpGet("static-map")]
    public async Task<IActionResult> GetStaticMapUrl(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int zoom = 14,
        [FromQuery] int width = 640,
        [FromQuery] int height = 480,
        [FromQuery] string? markers = null,
        [FromQuery] string? path = null)
    {
        var markerPoints = ParseLatLngList(markers);
        var pathPoints = ParseLatLngList(path);

        return Ok(await Mediator.Send(new GetStaticMapUrlQuery(
            lat, lng, zoom, width, height, markerPoints, pathPoints)));
    }

    [HttpGet("street-view")]
    public async Task<IActionResult> GetStreetViewUrl(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int width = 600,
        [FromQuery] int height = 400,
        [FromQuery] int? heading = null,
        [FromQuery] int? pitch = null,
        [FromQuery] int? fov = null)
        => Ok(await Mediator.Send(new GetStreetViewUrlQuery(lat, lng, width, height, heading, pitch, fov)));

    /// <summary>Places API (New) Nearby Search around a vehicle GPS fix.</summary>
    [HttpGet("nearby-places")]
    public async Task<IActionResult> GetNearbyPlaces(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] string category = "fuel",
        [FromQuery] int radiusMeters = 1500,
        [FromQuery] int maxResults = 8)
        => Ok(await Mediator.Send(new GetNearbyPlacesQuery(lat, lng, category, radiusMeters, maxResults)));

    /// <summary>
    /// Resolves a Places photo resource name to a browser image URL (Place Photos media).
    /// </summary>
    [HttpGet("place-photo")]
    public async Task<IActionResult> GetPlacePhoto(
        [FromQuery] string name,
        [FromQuery] int maxWidthPx = 800)
        => Ok(await Mediator.Send(new GetNearbyPlacePhotoQuery(name, maxWidthPx)));

    private static LatLngPoint? ParseLatLng(string token)
    {
        var parts = token.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return null;
        if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat))
            return null;
        if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lng))
            return null;
        return new LatLngPoint(lat, lng);
    }

    private static IReadOnlyList<LatLngPoint>? ParseLatLngList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var points = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLatLng)
            .OfType<LatLngPoint>()
            .ToList();

        return points.Count > 0 ? points : null;
    }
}
