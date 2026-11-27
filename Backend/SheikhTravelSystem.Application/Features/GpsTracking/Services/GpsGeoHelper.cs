using System.Text.Json;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class GpsGeoHelper
{
    private const double EarthRadiusKm = 6371.0;
    public const double MaxCircleRadiusMeters = 50_000;

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
        string? geoJson,
        double corridorWidthMeters = 0)
    {
        var type = (areaType ?? "circle").Trim().ToLowerInvariant();
        if (type is "circle" or "")
        {
            return IsInsideCircle(lat, lng, centerLat, centerLng, radiusMeters);
        }

        if (type == "corridor")
        {
            var line = TryParseLineString(geoJson);
            return line is { Count: >= 2 } && corridorWidthMeters > 0
                   && DistanceToPolylineMeters(lat, lng, line) <= corridorWidthMeters / 2;
        }

        var ring = TryParsePolygonRing(geoJson);
        if (ring == null || ring.Count < 3)
        {
            return false;
        }

        return IsInsidePolygon(lng, lat, ring);
    }

    /// <summary>
    /// Inside/outside plus unsigned distance (meters) to the zone boundary.
    /// Returns false for unusable geometry (circle radius ≤ 0, polygon &lt; 3 points / unparseable) so callers
    /// can skip the zone instead of treating it as "outside".
    /// </summary>
    public static bool TryEvaluateGeofence(
        double lat,
        double lng,
        string areaType,
        double centerLat,
        double centerLng,
        double radiusMeters,
        string? geoJson,
        out bool inside,
        out double boundaryDistanceMeters,
        double corridorWidthMeters = 0)
    {
        inside = false;
        boundaryDistanceMeters = 0;
        var type = (areaType ?? "circle").Trim().ToLowerInvariant();
        if (type is "circle" or "")
        {
            if (radiusMeters <= 0)
                return false;
            var d = HaversineKm(lat, lng, centerLat, centerLng) * 1000.0;
            inside = d <= radiusMeters;
            boundaryDistanceMeters = Math.Abs(d - radiusMeters);
            return true;
        }

        if (type == "corridor")
        {
            var line = TryParseLineString(geoJson);
            if (line is not { Count: >= 2 } || corridorWidthMeters <= 0)
                return false;
            var half = corridorWidthMeters / 2;
            var d = DistanceToPolylineMeters(lat, lng, line);
            inside = d <= half;
            boundaryDistanceMeters = Math.Abs(d - half);
            return true;
        }

        var ring = TryParsePolygonRing(geoJson);
        if (ring == null || ring.Count < 3)
            return false;

        inside = IsInsidePolygon(lng, lat, ring);
        boundaryDistanceMeters = DistanceToRingMeters(lat, lng, ring);
        return true;
    }

    /// <summary>Minimum distance (meters) from a point to the ring edges, using a local equirectangular projection.</summary>
    public static double DistanceToRingMeters(double lat, double lng, IReadOnlyList<(double Lng, double Lat)> ring)
    {
        const double metersPerDegLat = 111_320.0;
        var metersPerDegLng = metersPerDegLat * Math.Cos(DegreesToRadians(lat));
        var best = double.MaxValue;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var ax = (ring[j].Lng - lng) * metersPerDegLng;
            var ay = (ring[j].Lat - lat) * metersPerDegLat;
            var bx = (ring[i].Lng - lng) * metersPerDegLng;
            var by = (ring[i].Lat - lat) * metersPerDegLat;
            var dx = bx - ax;
            var dy = by - ay;
            var lenSq = dx * dx + dy * dy;
            var t = lenSq <= 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / lenSq, 0, 1);
            var px = ax + t * dx;
            var py = ay + t * dy;
            best = Math.Min(best, Math.Sqrt(px * px + py * py));
        }

        return best;
    }

    /// <summary>Minimum distance (meters) from a point to an open polyline (no closing edge).</summary>
    public static double DistanceToPolylineMeters(double lat, double lng, IReadOnlyList<(double Lng, double Lat)> line)
    {
        const double metersPerDegLat = 111_320.0;
        var metersPerDegLng = metersPerDegLat * Math.Cos(DegreesToRadians(lat));
        var best = double.MaxValue;
        for (var i = 1; i < line.Count; i++)
        {
            var ax = (line[i - 1].Lng - lng) * metersPerDegLng;
            var ay = (line[i - 1].Lat - lat) * metersPerDegLat;
            var bx = (line[i].Lng - lng) * metersPerDegLng;
            var by = (line[i].Lat - lat) * metersPerDegLat;
            var dx = bx - ax;
            var dy = by - ay;
            var lenSq = dx * dx + dy * dy;
            var t = lenSq <= 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / lenSq, 0, 1);
            var px = ax + t * dx;
            var py = ay + t * dy;
            best = Math.Min(best, Math.Sqrt(px * px + py * py));
        }

        return best;
    }

    public const double MinCorridorWidthMeters = 10;
    public const double MaxCorridorWidthMeters = 5_000;

    public static string NormalizeAreaType(string? areaType)
    {
        var t = (areaType ?? "circle").Trim().ToLowerInvariant();
        return t is "polygon" or "rectangle" or "corridor" ? t : "circle";
    }

    public static (double Lat, double Lng, double Radius) NormalizeGeometry(
        string areaType, double centerLat, double centerLng, double radiusMeters, string? geoJson)
    {
        if (areaType == "circle")
            return (centerLat, centerLng, radiusMeters);

        if (areaType == "corridor")
        {
            var line = TryParseLineString(geoJson);
            if (line is { Count: > 0 })
            {
                var c = CentroidOfRing(line);
                return (c.Lat, c.Lng, 0);
            }

            return (centerLat, centerLng, 0);
        }

        var ring = TryParsePolygonRing(geoJson);
        if (ring != null && ring.Count > 0)
        {
            var c = CentroidOfRing(ring);
            return (c.Lat, c.Lng, 0);
        }

        return (centerLat, centerLng, 0);
    }

    public static bool TryValidateGeofenceGeometry(string areaType, double radiusMeters, string? geoJson, out string? error)
        => TryValidateGeofenceGeometry(areaType, radiusMeters, geoJson, null, out error);

    public static bool TryValidateGeofenceGeometry(
        string areaType, double radiusMeters, string? geoJson, double? corridorWidthMeters, out string? error)
    {
        error = null;
        var type = (areaType ?? "circle").Trim().ToLowerInvariant();
        if (type == "corridor")
        {
            var line = TryParseLineString(geoJson);
            if (line is null || line.Distinct().Count() < 2)
            {
                error = "A corridor needs a GeoJSON line with at least 2 distinct points.";
                return false;
            }

            if (corridorWidthMeters is not { } w || w < MinCorridorWidthMeters || w > MaxCorridorWidthMeters)
            {
                error = $"Corridor width must be between {MinCorridorWidthMeters:0} and {MaxCorridorWidthMeters:0} m.";
                return false;
            }

            return true;
        }

        if (type is "circle")
        {
            if (radiusMeters <= 0)
            {
                error = "Radius must be greater than 0 for circle geofences.";
                return false;
            }
            if (radiusMeters > MaxCircleRadiusMeters)
            {
                error = $"Radius must be at most {MaxCircleRadiusMeters / 1000:0} km.";
                return false;
            }
            return true;
        }

        if (type is not ("polygon" or "rectangle"))
        {
            error = "AreaType must be circle, polygon, rectangle, or corridor.";
            return false;
        }

        var ring = TryParsePolygonRing(geoJson);
        if (ring == null)
        {
            error = "GeoJson polygon is required for polygon/rectangle geofences.";
            return false;
        }

        if (ring.Distinct().Count() < 3)
        {
            error = "Polygon must have at least 3 distinct vertices.";
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

    /// <summary>
    /// LineString points as (lng, lat) for corridors. Accepts Feature, FeatureCollection[0] or bare LineString geometry.
    /// </summary>
    public static IReadOnlyList<(double Lng, double Lat)>? TryParseLineString(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(geoJson);
            var geometry = UnwrapGeometry(doc.RootElement);
            if (!geometry.TryGetProperty("type", out var geomType)
                || !string.Equals(geomType.GetString(), "LineString", StringComparison.OrdinalIgnoreCase)
                || !geometry.TryGetProperty("coordinates", out var coords))
                return null;

            var points = new List<(double Lng, double Lat)>();
            foreach (var pt in coords.EnumerateArray())
            {
                if (pt.GetArrayLength() >= 2)
                    points.Add((pt[0].GetDouble(), pt[1].GetDouble()));
            }

            return points.Count >= 2 ? points : null;
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement UnwrapGeometry(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var rootType))
            return root;

        var t = rootType.GetString();
        if (string.Equals(t, "Feature", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("geometry", out var g))
            return g;

        if (string.Equals(t, "FeatureCollection", StringComparison.OrdinalIgnoreCase)
            && root.TryGetProperty("features", out var features)
            && features.GetArrayLength() > 0
            && features[0].TryGetProperty("geometry", out var fg))
            return fg;

        return root;
    }

    /// <summary>
    /// Area (m²) and perimeter (m) of a geofence. Circle: πr² / 2πr. Polygon and rectangle: spherical excess on the
    /// WGS84 mean radius. Corridor: a band of the given width along the line with round ends. Zero for unusable geometry.
    /// </summary>
    public static (double AreaSquareMeters, double PerimeterMeters) ComputeAreaAndPerimeter(
        string areaType, double radiusMeters, string? geoJson, double? corridorWidthMeters)
    {
        var type = NormalizeAreaType(areaType);
        if (type == "circle")
            return radiusMeters > 0 ? (Math.PI * radiusMeters * radiusMeters, 2 * Math.PI * radiusMeters) : (0, 0);

        if (type == "corridor")
        {
            var line = TryParseLineString(geoJson);
            var width = corridorWidthMeters ?? 0;
            if (line is not { Count: >= 2 } || width <= 0)
                return (0, 0);
            var length = 0.0;
            for (var i = 1; i < line.Count; i++)
                length += HaversineKm(line[i - 1].Lat, line[i - 1].Lng, line[i].Lat, line[i].Lng) * 1000.0;
            var half = width / 2;
            return (length * width + Math.PI * half * half, 2 * length + 2 * Math.PI * half);
        }

        var ring = TryParsePolygonRing(geoJson);
        if (ring is not { Count: >= 3 })
            return (0, 0);

        const double r = EarthRadiusKm * 1000.0;
        double sum = 0, perimeter = 0;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var (lng1, lat1) = ring[j];
            var (lng2, lat2) = ring[i];
            sum += DegreesToRadians(lng2 - lng1) * (2 + Math.Sin(DegreesToRadians(lat1)) + Math.Sin(DegreesToRadians(lat2)));
            perimeter += HaversineKm(lat1, lng1, lat2, lng2) * 1000.0;
        }

        return (Math.Abs(sum * r * r / 2.0), perimeter);
    }

    /// <summary>Bounding box of a geofence grown by <paramref name="marginMeters"/>; null for unusable geometry.</summary>
    public static (double MinLat, double MaxLat, double MinLng, double MaxLng)? TryGetBoundingBox(
        string areaType, double centerLat, double centerLng, double radiusMeters, string? geoJson,
        double? corridorWidthMeters, double marginMeters = 0)
    {
        var type = NormalizeAreaType(areaType);
        IReadOnlyList<(double Lng, double Lat)>? points;
        double grow = marginMeters;
        if (type == "circle")
        {
            if (radiusMeters <= 0)
                return null;
            points = [(centerLng, centerLat)];
            grow += radiusMeters;
        }
        else if (type == "corridor")
        {
            points = TryParseLineString(geoJson);
            grow += (corridorWidthMeters ?? 0) / 2;
        }
        else
        {
            points = TryParsePolygonRing(geoJson);
        }

        if (points is not { Count: > 0 })
            return null;

        var minLat = points.Min(p => p.Lat);
        var maxLat = points.Max(p => p.Lat);
        var minLng = points.Min(p => p.Lng);
        var maxLng = points.Max(p => p.Lng);
        var dLat = grow / 111_320.0;
        var dLng = grow / (111_320.0 * Math.Max(Math.Cos(DegreesToRadians((minLat + maxLat) / 2)), 0.01));
        return (minLat - dLat, maxLat + dLat, minLng - dLng, maxLng + dLng);
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
