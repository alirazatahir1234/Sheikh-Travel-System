using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class DriverScoreCalculatorExtendedTests
{
    [Fact]
    public void RewardEligible_top_twenty_percent()
    {
        // Ceiling(10 * 0.2) = 2 → ranks 1–2 eligible
        DriverScoreCalculator.RewardEligibleFlag(90, 1, 10).Should().BeTrue();
        DriverScoreCalculator.RewardEligibleFlag(90, 2, 10).Should().BeTrue();
        DriverScoreCalculator.RewardEligibleFlag(90, 3, 10).Should().BeFalse();
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

    [Fact]
    public void Overall_is_weighted_blend_of_dimensions()
    {
        var weights = new DriverScoreWeightsDto(40, 30, 30);
        // 100/70/40 → 0.4*100 + 0.3*70 + 0.3*40 = 40+21+12 = 73
        DriverScoreCalculator.ComputeOverallScore(100, 70, 40, weights).Should().Be(73);
    }

    [Fact]
    public void Overall_defaults_when_weights_zero()
    {
        var zero = new DriverScoreWeightsDto(0, 0, 0);
        DriverScoreCalculator.ComputeOverallScore(100, 100, 100, zero).Should().Be(100);
    }

    [Fact]
    public void Fuel_uses_peer_band_when_median_present()
    {
        // 8 km/L vs peer median 10 → ratio 0.8 → 50
        DriverScoreCalculator.ComputeFuelEfficiencyScore(80, 10m, peerMedianKmPerLiter: 10m)
            .Should().Be(50);
        // absolute fallback when no peer
        DriverScoreCalculator.ComputeFuelEfficiencyScore(120, 10m, peerMedianKmPerLiter: null)
            .Should().Be(100);
    }

    [Fact]
    public void Fuel_neutral_when_missing_data()
    {
        DriverScoreCalculator.ComputeFuelEfficiencyScore(100, null).Should().Be(70);
        DriverScoreCalculator.ComputeFuelEfficiencyScore(0, 10m).Should().Be(70);
    }

    [Fact]
    public void Behavior_score_is_secondary_not_overall()
    {
        var factors = new DriverScoreFactorsDto(5, 100, 0, 0, 0, 0, 20m);
        var behavior = DriverScoreCalculator.ComputeBehaviorScore(factors, idlePercent: 10);
        var overall = DriverScoreCalculator.ComputeOverallScore(100, 90, 80);
        behavior.Should().Be(100);
        overall.Should().Be(91); // 0.4*100 + 0.3*90 + 0.3*80 = 40+27+24 = 91
        overall.Should().NotBe(behavior);
    }

    [Fact]
    public void Median_even_and_odd()
    {
        DriverScoreCalculator.Median([1, 3, 5]).Should().Be(3);
        DriverScoreCalculator.Median([1, 2, 3, 4]).Should().Be(2.5m);
        DriverScoreCalculator.Median([]).Should().BeNull();
    }

    [Fact]
    public void Explanation_includes_delta_and_weights()
    {
        var factors = new DriverScoreFactorsDto(3, 50, 1, 0, 0, 0, 5m);
        var text = DriverScoreCalculator.BuildChangeExplanation(
            80, 75, 90, 70, 80, factors, fuelUsedPeerBand: false, kmPerLiter: 10m,
            DriverScoreCalculator.DefaultWeights());
        text.Should().Contain("80→75");
        text.Should().Contain("Safety 90");
        text.Should().Contain("Weights");
    }
}
