using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsDailyTripDetectorTests
{
    [Fact]
    public void DetectDailyTrip_FirstMoveToLastStop_OneTripPerDay()
    {
        var tz = TimeZoneInfo.Utc;
        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = day.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);

        var points = new List<PositionDto>
        {
            new(1, 1, null, null, null, 31.50, 74.30, 0, null, null, false, start),
            new(2, 1, null, null, null, 31.51, 74.31, 40, null, null, true, start.AddMinutes(10)),
            new(3, 1, null, null, null, 31.52, 74.32, 45, null, null, true, start.AddMinutes(20)),
            new(4, 1, null, null, null, 31.53, 74.33, 0, null, null, false, start.AddMinutes(40)),
            new(5, 1, null, null, null, 31.53, 74.33, 0, null, null, false, start.AddMinutes(50))
        };

        var trip = GpsDailyTripDetector.DetectDailyTrip(1, "Bus", null, points, day, tz);
        trip.Should().NotBeNull();
        trip!.StartTime.Should().Be(points[1].Timestamp);
        trip.EndTime.Should().Be(points[4].Timestamp);
        trip.DistanceKm.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DetectDailyTrip_NoMovement_ReturnsNull()
    {
        var tz = TimeZoneInfo.Utc;
        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = day.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        var points = new List<PositionDto>
        {
            new(1, 1, null, null, null, 31.5, 74.3, 0, null, null, false, start)
        };

        GpsDailyTripDetector.DetectDailyTrip(1, "Bus", null, points, day, tz).Should().BeNull();
    }
}
