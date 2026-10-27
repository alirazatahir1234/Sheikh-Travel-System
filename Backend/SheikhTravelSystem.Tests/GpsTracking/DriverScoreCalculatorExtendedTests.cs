using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class DriverScoreCalculatorExtendedTests
{
    [Fact]
    public void RewardEligible_top_twenty_percent()
    {
        DriverScoreCalculator.RewardEligibleFlag(90, 1, 10).Should().BeTrue();
        DriverScoreCalculator.RewardEligibleFlag(90, 3, 10).Should().BeTrue();
        DriverScoreCalculator.RewardEligibleFlag(90, 4, 10).Should().BeFalse();
    }

    [Fact]
    public void OnTime_score_from_completed_trips()
    {
        DriverScoreCalculator.ComputeOnTimeScore(0, 0).Should().Be(70);
        DriverScoreCalculator.ComputeOnTimeScore(10, 8).Should().Be(80);
    }

    [Fact]
    public void Safety_score_penalizes_events()
    {
        var factors = new DriverScoreFactorsDto(5, 100, 5, 0, 2, 0, null);
        DriverScoreCalculator.ComputeSafetyScore(factors).Should().BeLessThan(90);
    }
}
