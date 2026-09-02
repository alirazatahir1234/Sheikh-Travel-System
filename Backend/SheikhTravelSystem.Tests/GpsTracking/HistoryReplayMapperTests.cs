using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsDurationFormatterTests
{
    [Fact]
    public void FormatMinutes_Zero_ReturnsZeroMin()
    {
        Assert.Equal("0 min", GpsDurationFormatter.FormatMinutes(0));
    }

    [Fact]
    public void FormatMinutes_OnlyMinutes()
    {
        Assert.Equal("40 min", GpsDurationFormatter.FormatMinutes(40));
    }

    [Fact]
    public void FormatMinutes_OnlyHours()
    {
        Assert.Equal("23 hr", GpsDurationFormatter.FormatMinutes(23 * 60));
    }

    [Fact]
    public void FormatMinutes_HoursAndMinutes()
    {
        Assert.Equal("23 hr 40 min", GpsDurationFormatter.FormatMinutes(23 * 60 + 40));
    }
}

public class HistoryReplayMapperTests
{
    [Fact]
    public void SplitParkingStops_Uses120MinuteThreshold()
    {
        var stops = new List<TripStopDto>
        {
            new(DateTime.UtcNow, DateTime.UtcNow, 0, 0, null, 119),
            new(DateTime.UtcNow, DateTime.UtcNow, 0, 0, null, 120),
            new(DateTime.UtcNow, DateTime.UtcNow, 0, 0, null, 200),
        };

        var parking = HistoryReplayMapper.SplitParkingStops(stops);

        Assert.Equal(2, parking.Count);
        Assert.True(parking.All(s => s.DurationMinutes >= 120));
    }

    [Fact]
    public void DerivePositionStatus_FromSpeedAndIgnition()
    {
        Assert.Equal("moving", HistoryReplayMapper.DerivePositionStatus(22m, true));
        Assert.Equal("idle", HistoryReplayMapper.DerivePositionStatus(0m, true));
        Assert.Equal("stopped", HistoryReplayMapper.DerivePositionStatus(0m, false));
        Assert.Equal("stopped", HistoryReplayMapper.DerivePositionStatus(0m, null));
    }

    [Fact]
    public void BuildDisplaySummary_FormatsDurationsAndCounts()
    {
        var stats = new TripAnalyticsSummaryDto(
            TripCount: 1,
            DistanceKm: 210.3,
            DrivingMinutes: 23 * 60 + 40,
            IdleMinutes: 158 * 60 + 32,
            FuelLiters: null,
            AvgSpeedKmh: 35m,
            MaxSpeedKmh: 72m,
            StopCount: 40,
            OverspeedCount: 0,
            HarshBrakeCount: 0,
            HarshAccelCount: 0);

        var stops = new List<TripStopDto>
        {
            new(DateTime.UtcNow, DateTime.UtcNow, 0, 0, null, 30),
            new(DateTime.UtcNow, DateTime.UtcNow, 0, 0, null, 150),
        };

        var display = HistoryReplayMapper.BuildDisplaySummary(stats, stops, 210.3);

        Assert.Equal("23 hr 40 min", display.MovingTime);
        Assert.Equal("158 hr 32 min", display.NonMovingTime);
        Assert.Equal(40, display.Stops);
        Assert.Equal(1, display.Parking);
        Assert.Equal(72m, display.MaxSpeed);
        Assert.Equal(210.3, display.Distance);
    }
}
