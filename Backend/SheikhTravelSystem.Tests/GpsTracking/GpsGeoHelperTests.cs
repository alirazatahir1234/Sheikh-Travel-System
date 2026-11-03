using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsGeoHelperTests
{
    [Fact]
    public void IsInsideCircle_EdgeAndOutside()
    {
        Assert.True(GpsGeoHelper.IsInsideCircle(31.52, 74.35, 31.52, 74.35, 500));
        Assert.False(GpsGeoHelper.IsInsideCircle(31.55, 74.35, 31.52, 74.35, 500));
    }

    [Fact]
    public void IsInsidePolygon_SquareContainsCenter()
    {
        // Square around (0,0) in lng/lat
        var ring = new List<(double Lng, double Lat)>
        {
            (-1, -1),
            (1, -1),
            (1, 1),
            (-1, 1)
        };
        Assert.True(GpsGeoHelper.IsInsidePolygon(0, 0, ring));
        Assert.False(GpsGeoHelper.IsInsidePolygon(2, 0, ring));
    }

    [Fact]
    public void IsInsideGeofence_ParsesGeoJsonFeature()
    {
        const string geoJson = """
            {"type":"Feature","properties":{},"geometry":{"type":"Polygon","coordinates":[[
              [74.34,31.51],[74.36,31.51],[74.36,31.53],[74.34,31.53],[74.34,31.51]
            ]]}}
            """;

        Assert.True(GpsGeoHelper.IsInsideGeofence(31.52, 74.35, "polygon", 0, 0, 0, geoJson));
        Assert.False(GpsGeoHelper.IsInsideGeofence(31.6, 74.5, "polygon", 0, 0, 0, geoJson));
        Assert.True(GpsGeoHelper.IsInsideGeofence(31.52, 74.35, "rectangle", 0, 0, 0, geoJson));
    }

    [Fact]
    public void TryValidateGeofenceGeometry_RejectsInvalid()
    {
        Assert.False(GpsGeoHelper.TryValidateGeofenceGeometry("circle", 0, null, out var err1));
        Assert.Contains("Radius", err1!);

        Assert.False(GpsGeoHelper.TryValidateGeofenceGeometry("polygon", 0, null, out var err2));
        Assert.Contains("GeoJson", err2!);

        Assert.True(GpsGeoHelper.TryValidateGeofenceGeometry("circle", 100, null, out _));

        Assert.False(GpsGeoHelper.TryValidateGeofenceGeometry("polygon", 0, "not json", out _));
        const string twoPoints = """{"type":"Polygon","coordinates":[[[74.3,31.5],[74.4,31.5],[74.3,31.5]]]}""";
        Assert.False(GpsGeoHelper.TryValidateGeofenceGeometry("polygon", 0, twoPoints, out _));
    }

    [Fact]
    public void TryEvaluateGeofence_ReportsInsideAndBoundaryDistance()
    {
        const string square = """{"type":"Polygon","coordinates":[[[74.30,31.50],[74.40,31.50],[74.40,31.60],[74.30,31.60],[74.30,31.50]]]}""";

        Assert.True(GpsGeoHelper.TryEvaluateGeofence(31.55, 74.35, "polygon", 0, 0, 0, square, out var inside, out var dist));
        Assert.True(inside);
        Assert.InRange(dist, 4_500, 5_700); // ~0.05° to nearest edge

        Assert.True(GpsGeoHelper.TryEvaluateGeofence(31.52, 74.35, "circle", 31.52, 74.35, 200, null, out var inCircle, out var circleDist));
        Assert.True(inCircle);
        Assert.InRange(circleDist, 199, 201);
    }

    [Fact]
    public void TryEvaluateGeofence_RejectsInvalidGeometry()
    {
        Assert.False(GpsGeoHelper.TryEvaluateGeofence(31.5, 74.3, "polygon", 0, 0, 0, "{bad", out _, out _));
        Assert.False(GpsGeoHelper.TryEvaluateGeofence(31.5, 74.3, "circle", 31.5, 74.3, 0, null, out _, out _));
    }
}
