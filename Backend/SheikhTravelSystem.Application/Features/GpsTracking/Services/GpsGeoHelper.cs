using System.Text.Json;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class GpsGeoHelper
{
    private const double EarthRadiusKm = 6371.0;

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static bool IsInsideCircle(double lat, double lng, double centerLat, double centerLng, double radiusMeters)
        => HaversineKm(lat, lng, centerLat, centerLng) * 1000.0 <= radiusMeters;

    /// <summary>
    /// Point-in-geofence for circle / polygon / rectangle.
    /// Polygon and rectangle use GeoJSON Polygon rings (lng, lat order).
    /// </summary>
    public static bool IsInsideGeofence(
        double lat,
        double lng,
        string areaType,
        double centerLat,
        double centerLng,
        double radiusMeters,
        string? geoJson)
    {
        var type = (areaType ?? "circle").Trim().ToLowerInvariant();
        if (type is "circle" or "")
        {
            return IsInsideCircle(lat, lng, centerLat, centerLng, radiusMeters);
        }

        var ring = TryParsePolygonRing(geoJson);
        if (ring == null || ring.Count < 3)
        {
            return false;
        }

        return IsInsidePolygon(lng, lat, ring);
    }

    public static bool TryValidateGeofenceGeometry(string areaType, double radiusMeters, string? geoJson, out string? error)
    {
        error = null;
        var type = (areaType ?? "circle").Trim().ToLowerInvariant();
        if (type is "circle")
        {
            if (radiusMeters <= 0)
            {
                error = "Radius must be greater than 0 for circle geofences.";
                return false;
            }
            return true;
        }

        if (type is not ("polygon" or "rectangle"))
        {
            error = "AreaType must be circle, polygon, or rectangle.";
            return false;
        }

        var ring = TryParsePolygonRing(geoJson);
        if (ring == null)
        {
            error = "GeoJson polygon is required for polygon/rectangle geofences.";
            return false;
        }

        if (ring.Count < 3)
        {
            error = "Polygon must have at least 3 vertices.";
            return false;
        }

        if (type == "rectangle" && ring.Count < 4)
        {
            error = "Rectangle must have at least 4 corners.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns exterior ring as list of (lng, lat). Closes ring if needed for ray-casting.
    /// Accepts Feature, FeatureCollection[0], or bare Polygon geometry.
    /// </summary>
    public static IReadOnlyList<(double Lng, double Lat)>? TryParsePolygonRing(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(geoJson);
            var root = doc.RootElement;
            JsonElement geometry = root;

            if (root.TryGetProperty("type", out var rootType))
            {
                var t = rootType.GetString();
                if (string.Equals(t, "Feature", StringComparison.OrdinalIgnoreCase)
                    && root.TryGetProperty("geometry", out var g))
                {
                    geometry = g;
                }
                else if (string.Equals(t, "FeatureCollection", StringComparison.OrdinalIgnoreCase)
                         && root.TryGetProperty("features", out var features)
                         && features.GetArrayLength() > 0
                         && features[0].TryGetProperty("geometry", out var fg))
                {
                    geometry = fg;
                }
            }

            if (!geometry.TryGetProperty("type", out var geomType)
                || !string.Equals(geomType.GetString(), "Polygon", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!geometry.TryGetProperty("coordinates", out var coords) || coords.GetArrayLength() == 0)
            {
                return null;
            }

            var ringEl = coords[0];
            var points = new List<(double Lng, double Lat)>();
            foreach (var pt in ringEl.EnumerateArray())
            {
                if (pt.GetArrayLength() < 2)
                {
                    continue;
                }

                points.Add((pt[0].GetDouble(), pt[1].GetDouble()));
            }

            if (points.Count < 3)
            {
                return null;
            }

            // Drop duplicate closing vertex for counting; ray-cast works either way.
            if (points.Count > 1
                && Math.Abs(points[0].Lng - points[^1].Lng) < 1e-12
                && Math.Abs(points[0].Lat - points[^1].Lat) < 1e-12)
            {
                points.RemoveAt(points.Count - 1);
            }

            return points;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Ray-casting; x=lng, y=lat.</summary>
    public static bool IsInsidePolygon(double x, double y, IReadOnlyList<(double Lng, double Lat)> ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var xi = ring[i].Lng;
            var yi = ring[i].Lat;
            var xj = ring[j].Lng;
            var yj = ring[j].Lat;

            var intersect = ((yi > y) != (yj > y))
                            && (x < (xj - xi) * (y - yi) / ((yj - yi) + double.Epsilon) + xi);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static (double Lat, double Lng) CentroidOfRing(IReadOnlyList<(double Lng, double Lat)> ring)
    {
        double sumLat = 0, sumLng = 0;
        foreach (var (lng, lat) in ring)
        {
            sumLat += lat;
            sumLng += lng;
        }

        var n = Math.Max(ring.Count, 1);
        return (sumLat / n, sumLng / n);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
