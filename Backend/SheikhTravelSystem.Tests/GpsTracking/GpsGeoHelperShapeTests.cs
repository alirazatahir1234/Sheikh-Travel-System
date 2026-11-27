using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>Corridor shape, area/perimeter and bounding boxes added for the geofences workspace.</summary>
public class GpsGeoHelperShapeTests
{
    // An east–west road of about 1.1 km at Lahore's latitude.
    private const string Road = """{"type":"LineString","coordinates":[[74.30,31.50],[74.3117,31.50]]}""";

    [Fact]
    public void Corridor_inside_means_within_half_the_width_of_the_line()
    {
        // ~50 m north of the road, corridor 200 m wide → inside; ~150 m north → outside.
        GpsGeoHelper.TryEvaluateGeofence(31.50045, 74.305, "corridor", 0, 0, 0, Road, out var near, out var nearEdge, 200)
            .Should().BeTrue();
        near.Should().BeTrue();
        nearEdge.Should().BeApproximately(50, 2);

        GpsGeoHelper.TryEvaluateGeofence(31.50135, 74.305, "corridor", 0, 0, 0, Road, out var far, out _, 200)
            .Should().BeTrue();
        far.Should().BeFalse();

        GpsGeoHelper.IsInsideGeofence(31.50045, 74.305, "corridor", 0, 0, 0, Road, 200).Should().BeTrue();
    }

    [Fact]
    public void Corridor_needs_a_line_and_a_sane_width()
    {
        GpsGeoHelper.TryEvaluateGeofence(31.5, 74.3, "corridor", 0, 0, 0, Road, out _, out _, 0)
            .Should().BeFalse("no width");
        GpsGeoHelper.TryValidateGeofenceGeometry("corridor", 0, Road, 200, out var ok).Should().BeTrue(ok);
        GpsGeoHelper.TryValidateGeofenceGeometry("corridor", 0, Road, 5, out var narrow).Should().BeFalse();
        narrow.Should().Contain("width");
        GpsGeoHelper.TryValidateGeofenceGeometry("corridor", 0, """{"type":"LineString","coordinates":[[74.3,31.5]]}""", 200, out _)
            .Should().BeFalse("one point is not a line");
        GpsGeoHelper.NormalizeAreaType("Corridor").Should().Be("corridor");
    }

    [Fact]
    public void Circle_area_and_perimeter()
    {
        var (area, perimeter) = GpsGeoHelper.ComputeAreaAndPerimeter("circle", 100, null, null);

        area.Should().BeApproximately(Math.PI * 100 * 100, 0.01);
        perimeter.Should().BeApproximately(2 * Math.PI * 100, 0.01);
    }

    [Fact]
    public void Polygon_area_and_perimeter_of_a_small_square()
    {
        // ~100 m × ~100 m square near the equator.
        const string square =
            """{"type":"Polygon","coordinates":[[[0,0],[0.000898,0],[0.000898,0.000898],[0,0.000898],[0,0]]]}""";

        var (area, perimeter) = GpsGeoHelper.ComputeAreaAndPerimeter("polygon", 0, square, null);

        area.Should().BeApproximately(10_000, 150);
        perimeter.Should().BeApproximately(400, 2);
    }

    [Fact]
    public void Corridor_area_is_a_band_with_round_ends()
    {
        var (area, perimeter) = GpsGeoHelper.ComputeAreaAndPerimeter("corridor", 0, Road, 100);

        var length = GpsGeoHelper.HaversineKm(31.50, 74.30, 31.50, 74.3117) * 1000;
        area.Should().BeApproximately(length * 100 + Math.PI * 50 * 50, 1);
        perimeter.Should().BeApproximately(2 * length + 2 * Math.PI * 50, 1);
    }

    [Fact]
    public void Bounding_box_grows_by_radius_and_margin()
    {
        var box = GpsGeoHelper.TryGetBoundingBox("circle", 31.5, 74.3, 1000, null, null, marginMeters: 100);

        box.Should().NotBeNull();
        var (minLat, maxLat, _, _) = box!.Value;
        ((maxLat - minLat) / 2 * 111_320).Should().BeApproximately(1100, 1);
        GpsGeoHelper.TryGetBoundingBox("polygon", 0, 0, 0, "not json", null).Should().BeNull();
    }
}
