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
    }
}
