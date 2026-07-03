using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

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

    [Fact]
    public void TraccarTripMapper_ConvertsUnitsAndAddresses()
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var end = start.AddMinutes(11);
        var trip = new TraccarTrip(
            DeviceId: 11,
            DeviceName: "Jimi VG03",
            StartTime: start,
            EndTime: end,
            StartLat: 32.1,
            StartLon: 74.3,
            EndLat: 32.2,
            EndLon: 74.4,
            Distance: 3589.95,
            AverageSpeed: 10.43,
            MaxSpeed: 15,
            Duration: 660,
            StartAddress: "Pasrur",
            EndAddress: "Pasrur",
            DriverName: null);

        var dto = TraccarTripMapper.ToGpsTripDto(trip, 5, "Toyota Corolla", 3, "Jimi VG03");

        dto.VehicleId.Should().Be(5);
        dto.VehicleName.Should().Be("Toyota Corolla");
        dto.DeviceName.Should().Be("Jimi VG03");
        dto.DistanceKm.Should().BeApproximately(3.59, 0.01);
        dto.DurationMinutes.Should().Be(11);
        dto.AvgSpeedKmh.Should().BeApproximately(19.3m, 0.1m);
        dto.StartAddress.Should().Be("Pasrur");
        dto.EndAddress.Should().Be("Pasrur");
        dto.Status.Should().Be("Completed");
    }

    [Fact]
    public void DetectTrips_LocallyDetected_DefaultsStatusToCompleted()
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
        trips[0].Status.Should().Be("Completed");
    }

    [Fact]
    public void DetectStops_StationaryPeriodAboveThreshold_ReturnsStop()
    {
        var baseTime = DateTime.UtcNow.AddHours(-2);
        var points = new List<PositionDto>
        {
            new(1, 1, null, null, null, 31.50, 74.30, 40, null, null, true, baseTime),
            new(2, 1, null, null, null, 31.51, 74.31, 0, null, null, false, baseTime.AddMinutes(5)),
            new(3, 1, null, null, null, 31.51, 74.31, 0, null, null, false, baseTime.AddMinutes(10)),
            new(4, 1, null, null, null, 31.51, 74.31, 0, null, null, false, baseTime.AddMinutes(15)),
            new(5, 1, null, null, null, 31.52, 74.32, 45, null, null, true, baseTime.AddMinutes(16))
        };

        var stops = GpsStopDetector.DetectStops(points);

        stops.Should().ContainSingle();
        stops[0].DurationMinutes.Should().Be(10);
        stops[0].Latitude.Should().Be(31.51);
        stops[0].Longitude.Should().Be(74.31);
    }

    [Fact]
    public void DetectStops_BriefPauseBelowThreshold_ReturnsNoStop()
    {
        var baseTime = DateTime.UtcNow.AddHours(-1);
        var points = new List<PositionDto>
        {
            new(1, 1, null, null, null, 31.50, 74.30, 40, null, null, true, baseTime),
            new(2, 1, null, null, null, 31.51, 74.31, 0, null, null, false, baseTime.AddMinutes(2)),
            new(3, 1, null, null, null, 31.52, 74.32, 45, null, null, true, baseTime.AddMinutes(3))
        };

        var stops = GpsStopDetector.DetectStops(points);

        stops.Should().BeEmpty();
    }

    [Fact]
    public void DetectStops_FewerThanTwoPoints_ReturnsNoStop()
    {
        var points = new List<PositionDto>
        {
            new(1, 1, null, null, null, 31.50, 74.30, 0, null, null, false, DateTime.UtcNow)
        };

        GpsStopDetector.DetectStops(points).Should().BeEmpty();
    }
}
