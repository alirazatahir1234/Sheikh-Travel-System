using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsEnterpriseTests
{
    [Fact]
    public void HaversineKm_SamePoint_ReturnsZero()
    {
        var km = GpsGeoHelper.HaversineKm(31.5, 74.3, 31.5, 74.3);
        km.Should().Be(0);
    }

    [Fact]
    public void IsInsideCircle_PointAtCenter_IsInside()
    {
        GpsGeoHelper.IsInsideCircle(32.5356, 74.3639, 32.5356, 74.3639, 500).Should().BeTrue();
    }

    [Fact]
    public void ShouldAttemptTripPersistence_WhenSlowingFromMoving_ReturnsTrue()
    {
        GpsPositionIngestionHelper.ShouldAttemptTripPersistence(0, false, 60m).Should().BeTrue();
        GpsPositionIngestionHelper.ShouldAttemptTripPersistence(3, null, 50m).Should().BeTrue();
    }

    [Fact]
    public void ShouldAttemptTripPersistence_WhenStillMoving_ReturnsFalse()
    {
        GpsPositionIngestionHelper.ShouldAttemptTripPersistence(40, true, 45m).Should().BeFalse();
    }

    [Fact]
    public void DetectTrips_TwoMovingSegments_ReturnsAtLeastOneTrip()
    {
        var baseTime = DateTime.UtcNow.AddHours(-2);
        var points = new List<PositionDto>
        {
            new(1, 1, null, null, null, 31.50, 74.30, 0, null, null, false, baseTime),
            new(2, 1, null, null, null, 31.51, 74.31, 40, null, null, true, baseTime.AddMinutes(5)),
            new(3, 1, null, null, null, 31.52, 74.32, 50, null, null, true, baseTime.AddMinutes(10)),
            new(4, 1, null, null, null, 31.53, 74.33, 0, null, null, false, baseTime.AddMinutes(20)),
            new(5, 1, null, null, null, 31.53, 74.33, 0, null, null, false, baseTime.AddMinutes(30))
        };

        var trips = GpsTripDetector.DetectTrips(1, "Bus 1", null, points);
        trips.Should().NotBeEmpty();
        trips[0].DistanceKm.Should().BeGreaterThan(0);
    }
}
